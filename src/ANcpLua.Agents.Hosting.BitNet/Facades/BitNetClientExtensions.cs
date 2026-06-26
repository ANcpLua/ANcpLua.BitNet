using System.Diagnostics.CodeAnalysis;
using ANcpLua.Roslyn.Utilities;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ANcpLua.Agents.Hosting.BitNet;

/// <summary>
///     Qyl-prefixed BitNet client-to-agent and option-to-client facades.
/// </summary>
public static class BitNetClientExtensions
{
    /// <summary>Builds an <see cref="IChatClient" /> from <paramref name="options" />. Equivalent to <see cref="BitNetChatClientFactory.Create" />.</summary>
    public static IChatClient AsBitNetChatClient(this BitNetClientOptions options)
    {
        Guard.NotNull(options);
        return BitNetChatClientFactory.Create(options);
    }

    /// <summary>
    ///     Adapts a BitNet-backed <see cref="IChatClient" /> to a <see cref="ChatClientAgent" />.
    ///     Returns a type from <c>Microsoft.Agents.AI</c>, which is itself preview; callers on a
    ///     stable channel must explicitly opt in to <c>ANCPLBITNET001</c>.
    /// </summary>
    [Experimental("ANCPLBITNET001")]
    public static ChatClientAgent AsBitNetAgent(
        this IChatClient client,
        string? instructions = null,
        string? name = null,
        string? description = null,
        IList<AITool>? tools = null,
        ILoggerFactory? loggerFactory = null,
        IServiceProvider? services = null)
    {
        Guard.NotNull(client);

        var options = new ChatClientAgentOptions
        {
            Name = name,
            Description = description,
            ChatOptions = new ChatOptions { Instructions = instructions, Tools = tools }
        };
        return new ChatClientAgent(client, options, loggerFactory, services);
    }
}
