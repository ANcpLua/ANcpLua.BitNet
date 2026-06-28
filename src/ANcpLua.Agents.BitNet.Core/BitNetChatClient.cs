using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ANcpLua.Roslyn.Utilities;
using Microsoft.Extensions.AI;

namespace ANcpLua.Agents.Hosting.BitNet;

/// <summary>
///     A dependency-free <see cref="IChatClient" /> over an OpenAI-compatible
///     <c>/chat/completions</c> endpoint (the surface <c>bitnet.cpp</c> / <c>llama-server</c> exposes).
///     It speaks the wire protocol directly with <see cref="HttpClient" /> and
///     <c>System.Text.Json</c> — no provider SDK — so the only API a consumer sees is the
///     vendor-neutral <see cref="IChatClient" />. Because it builds the request body itself it emits
///     <c>max_tokens</c> natively, which is why no <c>max_completion_tokens</c> shim is needed.
/// </summary>
/// <remarks>
///     Streaming is response-buffered: <see cref="GetStreamingResponseAsync" /> awaits the full
///     completion and then yields it as update(s). BitNet is a small local model used as a
///     test-double, so token-by-token streaming is not a goal here.
/// </remarks>
public sealed class BitNetChatClient : IChatClient
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly Uri _chatCompletions;
    private readonly string _model;
    private readonly ChatClientMetadata _metadata;

    /// <summary>
    ///     Creates a client targeting <paramref name="endpoint" /> + <paramref name="apiPath" />
    ///     (e.g. <c>http://localhost:11434</c> + <c>/v1</c> → <c>/v1/chat/completions</c>).
    /// </summary>
    /// <param name="endpoint">Base URI of the <c>llama-server</c> process.</param>
    /// <param name="apiPath">OpenAI-compatible API path appended to <paramref name="endpoint" /> (e.g. <c>/v1</c>).</param>
    /// <param name="model">Model identifier sent in each request.</param>
    /// <param name="timeout">Per-request timeout. Ignored when <paramref name="httpClient" /> is supplied.</param>
    /// <param name="httpClient">Optional caller-owned <see cref="HttpClient" />; when null an internal one is created and disposed.</param>
    public BitNetChatClient(Uri endpoint, string apiPath, string model, TimeSpan? timeout = null, HttpClient? httpClient = null)
    {
        Guard.NotNull(endpoint);
        Guard.NotNullOrWhiteSpace(apiPath);
        Guard.NotNullOrWhiteSpace(model);

        // Mirror the OpenAI SDK's base = endpoint + apiPath, then append the resource path.
        var apiBase = new Uri(endpoint, apiPath);
        if (!apiBase.AbsoluteUri.EndsWith("/", StringComparison.Ordinal))
            apiBase = new Uri(apiBase.AbsoluteUri + "/");
        _chatCompletions = new Uri(apiBase, "chat/completions");

        _model = model;
        _ownsHttp = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = timeout ?? TimeSpan.FromSeconds(120) };
        _metadata = new ChatClientMetadata("bitnet", endpoint, model);
    }

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(messages);

        var request = BuildRequest(messages, options);
        using var content = new StringContent(JsonSerializer.Serialize(request, s_json), Encoding.UTF8, "application/json");
        using var httpResponse = await _http.PostAsync(_chatCompletions, content, cancellationToken).ConfigureAwait(false);
        httpResponse.EnsureSuccessStatusCode();

        var stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var completion = await JsonSerializer.DeserializeAsync<ChatCompletion>(stream, s_json, cancellationToken).ConfigureAwait(false)
                         ?? throw new InvalidOperationException("BitNet returned an empty chat-completion response.");

        var choice = completion.Choices is { Count: > 0 } ? completion.Choices[0] : null;
        var text = choice?.Message?.Content ?? string.Empty;

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
        {
            ResponseId = completion.Id,
            ModelId = completion.Model ?? _model,
            FinishReason = MapFinishReason(choice?.FinishReason),
            Usage = MapUsage(completion.Usage)
        };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        foreach (var update in response.ToChatResponseUpdates())
            yield return update;
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        Guard.NotNull(serviceType);
        if (serviceKey is not null) return null;
        if (serviceType == typeof(ChatClientMetadata)) return _metadata;
        return serviceType.IsInstanceOfType(this) ? this : null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }

    private ChatRequest BuildRequest(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var wireMessages = new List<RequestMessage>();
        foreach (var message in messages)
        {
            var role = string.IsNullOrEmpty(message.Role.Value) ? "user" : message.Role.Value;
            wireMessages.Add(new RequestMessage { Role = role, Content = message.Text });
        }

        return new ChatRequest
        {
            Model = options?.ModelId ?? _model,
            Messages = wireMessages,
            MaxTokens = options?.MaxOutputTokens,
            Temperature = options?.Temperature,
            TopP = options?.TopP,
            FrequencyPenalty = options?.FrequencyPenalty,
            PresencePenalty = options?.PresencePenalty,
            Seed = options?.Seed,
            Stop = options?.StopSequences is { Count: > 0 } stops ? new List<string>(stops) : null
        };
    }

    private static ChatFinishReason? MapFinishReason(string? reason) => reason switch
    {
        null or "" => null,
        "stop" => ChatFinishReason.Stop,
        "length" => ChatFinishReason.Length,
        "tool_calls" or "function_call" => ChatFinishReason.ToolCalls,
        "content_filter" => ChatFinishReason.ContentFilter,
        _ => new ChatFinishReason(reason)
    };

    private static UsageDetails? MapUsage(Usage? usage)
    {
        if (usage is null) return null;
        return new UsageDetails
        {
            InputTokenCount = usage.PromptTokens,
            OutputTokenCount = usage.CompletionTokens,
            TotalTokenCount = usage.TotalTokens
        };
    }

    // ---- OpenAI-compatible chat-completions DTOs (snake_case via JsonNamingPolicy) ----

    private sealed class ChatRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<RequestMessage> Messages { get; set; } = [];
        public int? MaxTokens { get; set; }
        public float? Temperature { get; set; }
        public float? TopP { get; set; }
        public float? FrequencyPenalty { get; set; }
        public float? PresencePenalty { get; set; }
        public long? Seed { get; set; }
        public List<string>? Stop { get; set; }
    }

    private sealed class RequestMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
    }

    private sealed class ChatCompletion
    {
        public string? Id { get; set; }
        public string? Model { get; set; }
        public List<Choice>? Choices { get; set; }
        public Usage? Usage { get; set; }
    }

    private sealed class Choice
    {
        public ResponseMessage? Message { get; set; }
        public string? FinishReason { get; set; }
    }

    private sealed class ResponseMessage
    {
        public string? Role { get; set; }
        public string? Content { get; set; }
    }

    private sealed class Usage
    {
        public int? PromptTokens { get; set; }
        public int? CompletionTokens { get; set; }
        public int? TotalTokens { get; set; }
    }
}
