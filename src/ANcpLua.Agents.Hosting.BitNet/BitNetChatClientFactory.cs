using ANcpLua.Roslyn.Utilities;
using Microsoft.Extensions.AI;

namespace ANcpLua.Agents.Hosting.BitNet;

/// <summary>
///     Builds an <see cref="IChatClient" /> bound to a <c>bitnet.cpp</c> <c>llama-server</c> endpoint.
///     The returned <see cref="BitNetChatClient" /> speaks the OpenAI-compatible
///     <c>/chat/completions</c> wire protocol directly over <see cref="System.Net.Http.HttpClient" /> —
///     no provider SDK — so consumers only ever see the vendor-neutral <see cref="IChatClient" />.
/// </summary>
public static class BitNetChatClientFactory
{
    /// <summary>
    ///     Constructs an <see cref="IChatClient" /> from <paramref name="options" />. Calls
    ///     <see cref="BitNetClientOptions.Validate" /> first so misconfiguration fails fast at
    ///     composition time.
    /// </summary>
    /// <param name="options">Endpoint configuration. Environment overrides must already be applied.</param>
    /// <returns>An <see cref="IChatClient" /> targeting <paramref name="options" />.<c>Endpoint</c> + <c>ApiPath</c>.</returns>
    public static IChatClient Create(BitNetClientOptions options)
    {
        Guard.NotNull(options);
        options.Validate();

        return BuildChatClient(options);
    }

    /// <summary>
    ///     Convenience overload: reads <see cref="BitNetClientOptions" /> defaults plus
    ///     environment-variable overrides (<c>BITNET_URL</c> / <c>BITNET_API_PATH</c> / <c>BITNET_MODEL</c>)
    ///     and returns an <see cref="IChatClient" /> when an endpoint is resolved, otherwise <see langword="null" />.
    /// </summary>
    /// <returns>An <see cref="IChatClient" /> when an endpoint is configured; otherwise <see langword="null" />.</returns>
    public static IChatClient? TryCreateFromEnvironment()
    {
        var options = new BitNetClientOptions().ApplyEnvironmentOverrides();
        if (options.Endpoint is null) return null;
        options.Validate();
        return BuildChatClient(options);
    }

    private static IChatClient BuildChatClient(BitNetClientOptions options) =>
        new BitNetChatClient(options.Endpoint!, options.ApiPath, options.Model, options.HttpClientTimeout);
}
