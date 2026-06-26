using System.ClientModel;
using System.ClientModel.Primitives;
using ANcpLua.Roslyn.Utilities;
using Microsoft.Extensions.AI;
using OpenAI;

namespace ANcpLua.Agents.Hosting.BitNet;

/// <summary>
///     Builds an <see cref="IChatClient" /> bound to a <c>bitnet.cpp</c> <c>llama-server</c>
///     endpoint using the OpenAI .NET SDK plus the <see cref="LegacyMaxTokensPolicy" /> shim.
/// </summary>
public static class BitNetChatClientFactory
{
    private const string UnusedApiKey = "unused";

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

    private static IChatClient BuildChatClient(BitNetClientOptions options)
    {
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(options.Endpoint!, options.ApiPath),
            NetworkTimeout = options.HttpClientTimeout
        };
        clientOptions.AddPolicy(new LegacyMaxTokensPolicy(), PipelinePosition.PerCall);

        var openAi = new OpenAIClient(new ApiKeyCredential(UnusedApiKey), clientOptions);
        return openAi.GetChatClient(options.Model).AsIChatClient();
    }
}
