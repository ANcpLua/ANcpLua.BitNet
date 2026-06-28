using System.Net;
using System.Text;
using ANcpLua.Agents.Hosting.BitNet;
using Microsoft.Extensions.AI;

namespace ANcpLua.Agents.Testing.BitNet.Tests;

/// <summary>
///     Verifies the dependency-free <see cref="BitNetChatClient" /> against a captured HTTP exchange:
///     it must POST a correct OpenAI-compatible request to <c>/chat/completions</c> and parse the
///     completion back into a <see cref="ChatResponse" />. No Docker, no server — pure wire-contract.
/// </summary>
public sealed class BitNetChatClientTests
{
    [Fact]
    public async Task GetResponseAsync_posts_an_openai_request_and_parses_the_completion()
    {
        const string Canned = """
            {"id":"cmpl-1","model":"bitnet-b1.58-2B-4T",
             "choices":[{"index":0,"message":{"role":"assistant","content":"pong"},"finish_reason":"stop"}],
             "usage":{"prompt_tokens":5,"completion_tokens":1,"total_tokens":6}}
            """;
        using var handler = new CapturingHandler(Canned);
        using var http = new HttpClient(handler, disposeHandler: false);
        using var client = new BitNetChatClient(new Uri("http://localhost:11434"), "/v1", "bitnet-test", httpClient: http);

        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "ping")],
            new ChatOptions { MaxOutputTokens = 16, Temperature = 0.2f });

        // Request: correct URL + an OpenAI-shaped, snake_cased body.
        Assert.Equal("http://localhost:11434/v1/chat/completions", handler.RequestUri!.ToString());
        Assert.NotNull(handler.Body);
        Assert.Contains("\"model\":\"bitnet-test\"", handler.Body);
        Assert.Contains("\"max_tokens\":16", handler.Body);
        Assert.Contains("\"role\":\"user\"", handler.Body);
        Assert.Contains("\"content\":\"ping\"", handler.Body);

        // Response: parsed into the neutral ChatResponse shape.
        Assert.Equal("pong", response.Text);
        Assert.Equal(ChatFinishReason.Stop, response.FinishReason);
        Assert.Equal("bitnet-b1.58-2B-4T", response.ModelId);
        Assert.NotNull(response.Usage);
        Assert.Equal(5L, response.Usage!.InputTokenCount);
        Assert.Equal(1L, response.Usage.OutputTokenCount);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_yields_the_buffered_text()
    {
        const string Canned =
            """{"choices":[{"message":{"role":"assistant","content":"pong"},"finish_reason":"stop"}]}""";
        using var handler = new CapturingHandler(Canned);
        using var http = new HttpClient(handler, disposeHandler: false);
        using var client = new BitNetChatClient(new Uri("http://localhost:11434"), "/v1", "bitnet-test", httpClient: http);

        var text = new StringBuilder();
        await foreach (var update in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "ping")]))
            text.Append(update.Text);

        Assert.Equal("pong", text.ToString());
    }

    [Fact]
    public void GetService_returns_metadata_for_the_bitnet_provider()
    {
        using var handler = new CapturingHandler("{}");
        using var http = new HttpClient(handler, disposeHandler: false);
        using var client = new BitNetChatClient(new Uri("http://localhost:11434"), "/v1", "bitnet-test", httpClient: http);

        var metadata = client.GetService(typeof(ChatClientMetadata)) as ChatClientMetadata;

        Assert.NotNull(metadata);
        Assert.Equal("bitnet", metadata!.ProviderName);
        Assert.Equal("bitnet-test", metadata.DefaultModelId);
    }

    private sealed class CapturingHandler(string responseJson) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
