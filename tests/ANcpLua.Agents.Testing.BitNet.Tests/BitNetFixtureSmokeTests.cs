using System.Diagnostics;
using System.Runtime.CompilerServices;
using ANcpLua.Agents.Testing.BitNet;
using Microsoft.Extensions.AI;

namespace ANcpLua.Agents.Tests.Testing;

/// <summary>
///     End-to-end smoke test of the auto-managed Docker path. Locally gated by the
///     <see cref="DockerEnabledFactAttribute" /> so machines without Docker just skip cleanly.
///     Not appropriate for unit-test CI lanes; the test pulls a ~1.6 GB image on cold runs.
/// </summary>
public sealed class BitNetFixtureSmokeTests
{
    [DockerEnabledFact]
    public async Task BitNetFixture_AutoDocker_RoundtripsAChatMessageAsync()
    {
        // Arrange — preconditions live entirely in the host env (see DockerEnabledFact + README).
        // The gate guarantees BITNET_URL is unset and BITNET_FIXTURE_NO_DOCKER is not truthy, so
        // BitNetFixture must take the auto-Docker branch. We do not mutate those env vars from
        // inside the test to keep it hermetic against parallel xUnit collections.
        var fixture = new BitNetFixture();
        try
        {
            // Act
            await fixture.InitializeAsync();

            // Narrow locally so the compiler's null-state tracker is satisfied without `!`.
            var endpoint = fixture.Endpoint;
            var chat = fixture.ChatClient;

            // Assert — fixture surface
            fixture.IsAvailable.Should().BeTrue("auto-Docker should have started a container and probed /health");
            endpoint.Should().NotBeNull();
            chat.Should().NotBeNull();
            if (endpoint is null || chat is null) return; // unreachable after asserts above, narrows for analyzer
            endpoint.Port.Should().Be(BitNetFixture.DockerPort);

            // Act — round-trip a single chat message
            var response = await chat.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "Reply with the single word: pong.")],
                new ChatOptions { MaxOutputTokens = 16 });

            // Assert — model produced a real, terminated response
            response.Text.Should().NotBeNullOrWhiteSpace();
            response.FinishReason.Should().Be(ChatFinishReason.Stop);
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }
}

/// <summary>
///     xUnit v3 Fact gate that runs only when both
///     <c>BITNET_SMOKE_TEST</c> is truthy <em>and</em> the <c>docker</c> binary on PATH responds.
///     Off by default so unit-test CI lanes don't pull a 1.6 GB image; opt in locally with
///     <c>BITNET_SMOKE_TEST=1 dotnet run -- -method '*BitNetFixture_AutoDocker*'</c>.
///     Carries <see cref="CallerFilePathAttribute"/> + <see cref="CallerLineNumberAttribute"/>
///     to satisfy xUnit3003 (source-info ctor).
/// </summary>
internal sealed class DockerEnabledFactAttribute : FactAttribute
{
    public DockerEnabledFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath ?? string.Empty, sourceLineNumber)
    {
        if (!IsTruthy(Environment.GetEnvironmentVariable("BITNET_SMOKE_TEST")))
        {
            Skip = "Set BITNET_SMOKE_TEST=1 to opt in to the auto-Docker BitNet fixture smoke test (pulls a ~1.6 GB image on cold runs).";
            return;
        }

        // BitNetFixture only enters the auto-Docker branch when BITNET_URL is unset AND
        // BITNET_FIXTURE_NO_DOCKER is not truthy. If either is set, this test would silently
        // exercise the wrong path; refuse rather than produce a false-pass.
        if (Environment.GetEnvironmentVariable("BITNET_URL") is { Length: > 0 })
        {
            Skip = "BITNET_URL is set — BitNetFixture would use that endpoint instead of auto-Docker. Unset it (or run this test in a clean shell) to exercise the auto-Docker path.";
            return;
        }

        if (IsTruthy(Environment.GetEnvironmentVariable("BITNET_FIXTURE_NO_DOCKER")))
        {
            Skip = "BITNET_FIXTURE_NO_DOCKER is truthy — BitNetFixture is opted out of auto-Docker. Unset it to exercise this test.";
            return;
        }

        if (!IsDockerAvailable())
        {
            Skip = "Docker not available on PATH — auto-Docker BitNetFixture path is local-dev only.";
        }
    }

    private static bool IsTruthy(string? value) =>
        value is { Length: > 0 } && value switch
        {
            "1" => true,
            _ when string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) => true,
            _ when string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) => true,
            _ when string.Equals(value, "on", StringComparison.OrdinalIgnoreCase) => true,
            _ => false
        };

    private static bool IsDockerAvailable()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("docker")
                {
                    ArgumentList = { "version", "--format", "{{.Server.Version}}" },
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            if (!process.Start()) return false;
            if (!process.WaitForExit(5_000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
                return false;
            }
            return process.ExitCode is 0;
        }
        catch
        {
            return false;
        }
    }
}
