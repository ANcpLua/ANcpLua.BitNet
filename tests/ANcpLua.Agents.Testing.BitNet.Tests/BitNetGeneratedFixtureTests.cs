using ANcpLua.Agents.Testing.BitNet;
using Microsoft.Extensions.AI;

namespace ANcpLua.Agents.Testing.BitNet.Tests;

/// <summary>
///     Exercises the <c>[BitNet]</c> fixture generator end-to-end: this class writes no fixture
///     boilerplate. The generator supplies the <c>[Collection]</c> attribute, the
///     <see cref="BitNetFixture" /> constructor, the <c>BitNet</c> accessor, and
///     <c>SkipUnlessBitNetAvailable()</c>, plus the <c>[CollectionDefinition]</c> in this assembly.
///     The round-trip test is Docker-gated and skips when no BitNet server is available (e.g. CI).
/// </summary>
[BitNet]
public partial class BitNetGeneratedFixtureTests
{
    [Fact]
    public void Generator_wires_the_shared_fixture()
    {
        // 'BitNet' is the generated accessor. That xUnit can construct this class at all proves the
        // generated [Collection] + [CollectionDefinition] + constructor injection line up — the
        // fixture is supplied even when the server itself is down.
        Assert.NotNull(BitNet);
    }

    [Fact]
    public async Task ChatClient_round_trips_when_a_server_is_available()
    {
        SkipUnlessBitNetAvailable();

        Assert.NotNull(BitNet.ChatClient);
        var reply = await BitNet.ChatClient!.GetResponseAsync("Reply with exactly: pong");
        Assert.False(string.IsNullOrWhiteSpace(reply.Text));
    }
}
