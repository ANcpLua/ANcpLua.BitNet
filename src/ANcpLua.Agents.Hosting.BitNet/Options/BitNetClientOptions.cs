using ANcpLua.Roslyn.Utilities;

namespace ANcpLua.Agents.Hosting.BitNet;

/// <summary>
///     Configuration for a single <c>bitnet.cpp</c> <c>llama-server</c> endpoint exposed as an
///     OpenAI-compatible chat client. Bound from <c>BitNet:&lt;name&gt;:*</c> configuration sections
///     by <see cref="BitNetHostingExtensions.AddBitNetChatClient(Microsoft.Extensions.Hosting.IHostApplicationBuilder, string, System.Action{BitNetClientOptions}?)" />.
/// </summary>
/// <remarks>
///     <para>Environment-variable overrides resolved by <see cref="ApplyEnvironmentOverrides" /> take
///     precedence over bound configuration so the existing <see cref="BitNetClientOptions" />
///     contract used by the <c>BitNetFixture</c> (<c>BITNET_URL</c> / <c>BITNET_API_PATH</c> /
///     <c>BITNET_MODEL</c>) keeps working.</para>
/// </remarks>
public sealed class BitNetClientOptions
{
    /// <summary>Default OpenAI-compatible API path served by <c>llama-server</c>.</summary>
    public const string DefaultApiPath = "/v1";

    /// <summary>Default GGUF weight family — Microsoft BitNet b1.58 2B 4T.</summary>
    public const string DefaultModel = "bitnet-b1.58-2B-4T";

    /// <summary>Environment-variable name probed in <see cref="ApplyEnvironmentOverrides" /> for <see cref="Endpoint" />.</summary>
    public const string EndpointEnvironmentVariable = "BITNET_URL";

    /// <summary>Environment-variable name probed in <see cref="ApplyEnvironmentOverrides" /> for <see cref="ApiPath" />.</summary>
    public const string ApiPathEnvironmentVariable = "BITNET_API_PATH";

    /// <summary>Environment-variable name probed in <see cref="ApplyEnvironmentOverrides" /> for <see cref="Model" />.</summary>
    public const string ModelEnvironmentVariable = "BITNET_MODEL";

    /// <summary>
    ///     Base URI of the <c>llama-server</c> process — e.g. <c>http://localhost:8080</c>. The
    ///     OpenAI-compat client is constructed against <see cref="Endpoint" /> + <see cref="ApiPath" />.
    /// </summary>
    public Uri? Endpoint { get; set; }

    /// <summary>OpenAI-compatible API path appended to <see cref="Endpoint" />. Defaults to <c>/v1</c>.</summary>
    public string ApiPath { get; set; } = DefaultApiPath;

    /// <summary>Model identifier sent in completion requests. Defaults to <see cref="DefaultModel" />.</summary>
    public string Model { get; set; } = DefaultModel;

    /// <summary>
    ///     Per-request HTTP timeout for chat completion calls. Defaults to 120 seconds — BitNet
    ///     generation against modest hardware can be slow.
    /// </summary>
    public TimeSpan HttpClientTimeout { get; set; } = TimeSpan.FromSeconds(120);

    /// <summary>Timeout for the <c>/health</c> probe used by <see cref="BitNetHealthCheck" />. Defaults to 3 seconds.</summary>
    public TimeSpan HealthProbeTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    ///     When <see langword="true" />, registers the <c>Microsoft.Extensions.AI</c>
    ///     <c>OpenTelemetryChatClient</c> decorator. Default: <see langword="true" />.
    /// </summary>
    public bool EnableOpenTelemetry { get; set; } = true;

    /// <summary>
    ///     Source-name applied to OpenTelemetry instrumentation. Defaults to
    ///     <c>ANcpLua.Agents.Hosting.BitNet</c>.
    /// </summary>
    public string OpenTelemetrySourceName { get; set; } = "ANcpLua.Agents.Hosting.BitNet";

    /// <summary>
    ///     Reads <c>BITNET_URL</c>, <c>BITNET_API_PATH</c>, and <c>BITNET_MODEL</c> from the current
    ///     process environment and overwrites the corresponding properties when set. Returns the
    ///     same instance for chaining.
    /// </summary>
    public BitNetClientOptions ApplyEnvironmentOverrides()
    {
        if (Environment.GetEnvironmentVariable(EndpointEnvironmentVariable) is { Length: > 0 } url)
            Endpoint = new Uri(url);
        if (Environment.GetEnvironmentVariable(ApiPathEnvironmentVariable) is { Length: > 0 } apiPath)
            ApiPath = apiPath;
        if (Environment.GetEnvironmentVariable(ModelEnvironmentVariable) is { Length: > 0 } model)
            Model = model;
        return this;
    }

    /// <summary>
    ///     Validates that <see cref="Endpoint" /> is set and <see cref="Model" /> is non-empty.
    ///     Throws <see cref="InvalidOperationException" /> otherwise.
    /// </summary>
    public void Validate()
    {
        Guard.NotNull(this);
        if (Endpoint is null)
            throw new InvalidOperationException(
                $"BitNet endpoint is not configured. Set ConnectionStrings:<name>, BitNet:<name>:Endpoint, " +
                $"or the {EndpointEnvironmentVariable} environment variable.");
        if (string.IsNullOrWhiteSpace(Model))
            throw new InvalidOperationException("BitNet model is not configured.");
        if (string.IsNullOrWhiteSpace(ApiPath))
            throw new InvalidOperationException("BitNet API path is not configured.");
    }
}
