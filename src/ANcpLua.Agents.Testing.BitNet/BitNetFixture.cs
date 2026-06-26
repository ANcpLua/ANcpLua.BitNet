using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Text.Json.Nodes;
using ANcpLua.Roslyn.Utilities;
using Microsoft.Extensions.AI;
using OpenAI;
using Xunit;

namespace ANcpLua.Agents.Testing.BitNet;

/// <summary>
///     Shared fixture that exposes an <see cref="IChatClient" /> wired to a BitNet
///     <c>llama-server</c>. Tests should guard with
///     <c>Skip.IfNot(bitnet.IsAvailable, "BitNet not running")</c>.
/// </summary>
/// <remarks>
///     <para>Endpoint discovery, in priority order:</para>
///     <list type="number">
///         <item>
///             <c>BITNET_URL</c> environment variable — if set, the fixture probes that endpoint
///             only. The caller is responsible for managing the server lifecycle.
///         </item>
///         <item>
///             <strong>Auto-managed Docker container.</strong> When <c>BITNET_URL</c> is unset and
///             <c>BITNET_FIXTURE_NO_DOCKER</c> is not truthy, the fixture starts Microsoft's
///             prebuilt BitNet image (<see cref="DockerImage" />) via <c>docker run</c> on port
///             <see cref="DockerPort" />, probes <c>/health</c>, and tears the container down in
///             <see cref="DisposeAsync" />. The container is pinned by digest for reproducibility.
///         </item>
///         <item>
///             Legacy fallback to <c>http://localhost:8080</c>. Probed only when Docker is
///             unavailable or auto-Docker is opted out — kept so older
///             <c>scripts/bitnet-docker.sh</c>-less setups still work if a server happens to be
///             listening there.
///         </item>
///     </list>
///     <para>Additional overrides honored once an endpoint is settled:</para>
///     <list type="bullet">
///         <item><c>BITNET_API_PATH</c> — overrides the default OpenAI-compatible API path (<c>/v1</c>).</item>
///         <item><c>BITNET_MODEL</c> — overrides the default model id (<c>bitnet-b1.58-2B-4T</c>).</item>
///     </list>
///     <para>This fixture intentionally does not depend on the
///     <c>ANcpLua.Agents.Hosting.BitNet</c> package — Testing is on the stable channel and BitNet
///     hosting is alpha, so they cannot share a runtime <c>PackageReference</c>. The private
///     <see cref="LegacyMaxTokensPolicy" /> below duplicates the public policy in
///     <c>ANcpLua.Agents.Hosting.BitNet</c> intentionally; both files are ~50 lines and never
///     drift in lockstep because each is scoped to its own assembly boundary.</para>
/// </remarks>
public sealed class BitNetFixture : IAsyncLifetime
{
    /// <summary>
    ///     Microsoft's prebuilt BitNet b1.58-2B-4T inference image, pinned by digest. Re-resolve
    ///     with <c>docker buildx imagetools inspect</c> if you intentionally want a newer build.
    /// </summary>
    public const string DockerImage =
        "mcr.microsoft.com/appsvc/docs/sidecars/sample-experiment@sha256:9d5f7f4e6e5a456b40582f7b00a70a5e2a4637c37f0976bfcffd1ed252cd243a";

    /// <summary>Host port the auto-managed Docker container binds to. Maps to container port 11434.</summary>
    public const int DockerPort = 11434;

    private const string DefaultApiPath = "/v1";
    private const string DefaultModel = "bitnet-b1.58-2B-4T";
    private const string UnusedApiKey = "unused";
    private const string OptOutEnvironmentVariable = "BITNET_FIXTURE_NO_DOCKER";
    private const string ContainerNamePrefix = "bitnet-fixture-";

    private static readonly Uri s_legacyFallbackEndpoint = new("http://localhost:8080");

    private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>
    ///     Name of the Docker container the fixture started, when applicable. <see langword="null" />
    ///     when the user supplied <c>BITNET_URL</c> or opted out of auto-Docker.
    /// </summary>
    private string? _ownedContainerName;

    /// <summary>Chat client connected to the BitNet server. Only usable when <see cref="IsAvailable" /> is <see langword="true" />.</summary>
    public IChatClient? ChatClient { get; private set; }

    /// <summary>Whether the BitNet server responded to the health probe during initialization.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>Endpoint the fixture probed and (when available) built <see cref="ChatClient" /> against.</summary>
    public Uri? Endpoint { get; private set; }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        Endpoint = await ResolveEndpointAsync().ConfigureAwait(false);
        if (Endpoint is null) return;

        IsAvailable = await ProbeHealthAsync(Endpoint).ConfigureAwait(false);
        if (!IsAvailable) return;

        var apiPath = Environment.GetEnvironmentVariable("BITNET_API_PATH") is { Length: > 0 } configuredApiPath
            ? configuredApiPath
            : DefaultApiPath;
        var model = Environment.GetEnvironmentVariable("BITNET_MODEL") is { Length: > 0 } configuredModel
            ? configuredModel
            : DefaultModel;

        var options = new OpenAIClientOptions { Endpoint = new Uri(Endpoint, apiPath) };
        options.AddPolicy(new LegacyMaxTokensPolicy(), PipelinePosition.PerCall);
        var client = new OpenAIClient(new ApiKeyCredential(UnusedApiKey), options);
        ChatClient = client.GetChatClient(model).AsIChatClient();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_ownedContainerName is not null)
        {
            // Best-effort stop — never throw out of teardown.
            await RunDockerAsync(["stop", _ownedContainerName], TimeSpan.FromSeconds(15)).ConfigureAwait(false);
        }

        _http.Dispose();
        ChatClient?.Dispose();
    }

    private async ValueTask<Uri?> ResolveEndpointAsync()
    {
        // Priority 1: caller-supplied BITNET_URL — we never touch Docker in this mode. A malformed
        // URL is a user-config error, not a fallback signal: surface it via UriFormatException so
        // the operator sees the actual cause rather than a silent switch to a different branch.
        if (Environment.GetEnvironmentVariable("BITNET_URL") is { Length: > 0 } url)
        {
            return new Uri(url, UriKind.Absolute);
        }

        // Priority 2: auto-managed Docker container, unless explicitly opted out.
        if (!IsTruthy(Environment.GetEnvironmentVariable(OptOutEnvironmentVariable))
            && await IsDockerAvailableAsync().ConfigureAwait(false))
        {
            _ownedContainerName = await StartContainerAsync().ConfigureAwait(false);
            if (_ownedContainerName is not null)
            {
                return new Uri($"http://localhost:{DockerPort}");
            }
        }

        // Priority 3: historical default. Probed in case a manually-run server happens to be there.
        return s_legacyFallbackEndpoint;
    }

    private async ValueTask<bool> ProbeHealthAsync(Uri endpoint)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var response = await _http.GetAsync(new Uri(endpoint, "/health"), cts.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async ValueTask<string?> StartContainerAsync()
    {
        // Separate image transfer from the /health budget below: a cold 1.6 GB pull alone can
        // exceed 60 s, exhausting the readiness loop before the model even starts loading.
        // `docker image inspect` against the pinned digest is a fast local lookup — only pull
        // when missing.
        var inspect = await RunDockerAsync(
            ["image", "inspect", DockerImage],
            TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        if (inspect.ExitCode is not 0)
        {
            var pull = await RunDockerAsync(
                ["pull", DockerImage],
                TimeSpan.FromMinutes(10)).ConfigureAwait(false);
            if (pull.ExitCode is not 0)
            {
                return null;
            }
        }

        var containerName = ContainerNamePrefix + Guid.NewGuid().ToString("N")[..8];

        var run = await RunDockerAsync(
            ["run", "-d", "--rm", "--name", containerName, "-p", $"{DockerPort}:11434", DockerImage],
            TimeSpan.FromMinutes(5)).ConfigureAwait(false);

        if (run.ExitCode is not 0)
        {
            // Most likely: port conflict, or rate-limit on a cache miss the pull above didn't
            // cover (e.g. concurrent test runs). Caller falls through to the legacy fallback
            // and ultimately reports IsAvailable=false.
            return null;
        }

        // Wait up to 60 s for /health — first model load on emulated hosts can be slow.
        var endpoint = new Uri($"http://localhost:{DockerPort}");
        for (var i = 0; i < 60; i++)
        {
            if (await ProbeHealthAsync(endpoint).ConfigureAwait(false))
            {
                return containerName;
            }

            await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }

        // Container never reached ready — clean up so we don't leak it.
        await RunDockerAsync(["stop", containerName], TimeSpan.FromSeconds(15)).ConfigureAwait(false);
        return null;
    }

    private static async ValueTask<bool> IsDockerAvailableAsync()
    {
        var result = await RunDockerAsync(
            ["version", "--format", "{{.Server.Version}}"],
            TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        return result.ExitCode is 0;
    }

    private static async ValueTask<(int ExitCode, string Stdout, string Stderr)> RunDockerAsync(
        IReadOnlyList<string> args,
        TimeSpan timeout)
    {
        using var process = new Process
        {
            StartInfo = BuildStartInfo(args)
        };

        try
        {
            if (!process.Start())
            {
                return (-1, string.Empty, "docker process failed to start");
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // docker binary not on PATH, or fork failed. Treat as unavailable, not fatal.
            return (-1, string.Empty, ex.Message);
        }

        using var cts = new CancellationTokenSource(timeout);
        // Drain stdout/stderr concurrently with WaitForExitAsync. `docker pull` on a cold host
        // emits substantial progress to stderr; if we waited on exit before reading, a full pipe
        // would block the child process and the wait would always hit the timeout path.
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

        try
        {
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            // Drain the pipes after kill so the underlying StreamReader and pipe handles release
            // cleanly. Discard the values — the timeout message carries enough diagnostic signal.
            try { _ = await stdoutTask.ConfigureAwait(false); } catch { /* best-effort */ }
            try { _ = await stderrTask.ConfigureAwait(false); } catch { /* best-effort */ }
            return (-1, string.Empty, $"docker timed out after {timeout}");
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        return (process.ExitCode, stdout, stderr);
    }

    private static ProcessStartInfo BuildStartInfo(IReadOnlyList<string> args)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        return startInfo;
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
}

/// <summary>
///     Mirrors <c>max_completion_tokens</c> → <c>max_tokens</c> for older llama-server builds
///     (pre ggml-org/llama.cpp PR #19831, merged 2026-02-23). Duplicates the public policy in
///     <c>ANcpLua.Agents.Hosting.BitNet</c> intentionally — Testing is on the stable channel and
///     cannot take a runtime dep on alpha-channel BitNet hosting.
/// </summary>
internal sealed class LegacyMaxTokensPolicy : PipelinePolicy
{
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        Mirror(message);
        ProcessNext(message, pipeline, currentIndex);
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        await MirrorAsync(message).ConfigureAwait(false);
        await ProcessNextAsync(message, pipeline, currentIndex).ConfigureAwait(false);
    }

    private static void Mirror(PipelineMessage message)
    {
        if (!ShouldMirror(message, out var content)) return;
        using var buffer = new MemoryStream();
        content.WriteTo(buffer, default);
        Apply(message, buffer);
    }

    private static async ValueTask MirrorAsync(PipelineMessage message)
    {
        if (!ShouldMirror(message, out var content)) return;
        await using var buffer = new MemoryStream();
        await content.WriteToAsync(buffer, message.CancellationToken).ConfigureAwait(false);
        Apply(message, buffer);
    }

    private static bool ShouldMirror(PipelineMessage message, out BinaryContent content)
    {
        // CS8625: `out BinaryContent` must be assigned on every return path. Callers must check
        // the bool before reading `content`; the false-returning branches never expose the null.
        content = null!;
        if (message.Request?.Content is not { } c) return false;
        if (message.Request.Uri?.AbsolutePath?.EndsWithOrdinal("/chat/completions") is not true) return false;
        content = c;
        return true;
    }

    private static void Apply(PipelineMessage message, MemoryStream buffer)
    {
        buffer.Position = 0;
        if (JsonNode.Parse(buffer) is not JsonObject body) return;
        if (body["max_tokens"] is null && body["max_completion_tokens"] is { } mct)
        {
            body["max_tokens"] = mct.DeepClone();
            message.Request.Content = BinaryContent.Create(BinaryData.FromString(body.ToJsonString()));
        }
    }
}
