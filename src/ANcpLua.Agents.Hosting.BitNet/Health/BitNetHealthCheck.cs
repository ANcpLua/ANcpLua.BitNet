using ANcpLua.Roslyn.Utilities;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ANcpLua.Agents.Hosting.BitNet;

/// <summary>
///     Probes the <c>/health</c> endpoint of a <c>bitnet.cpp</c> <c>llama-server</c> with the
///     timeout from <see cref="BitNetClientOptions.HealthProbeTimeout" />.
/// </summary>
/// <remarks>
///     Resolves named options via <see cref="IOptionsMonitor{TOptions}.Get(string)" /> so a single
///     health-check service can probe many endpoints registered under different connection names.
///     The shared <see cref="HttpClient" /> is short-lived (per-probe) because <c>llama-server</c>
///     does not keep-alive long idle TCP connections reliably.
/// </remarks>
public sealed class BitNetHealthCheck : IHealthCheck
{
    private readonly string _connectionName;
    private readonly IOptionsMonitor<BitNetClientOptions> _optionsMonitor;

    /// <summary>
    ///     Creates a health-check bound to the options registered under
    ///     <paramref name="connectionName" />.
    /// </summary>
    public BitNetHealthCheck(string connectionName, IOptionsMonitor<BitNetClientOptions> optionsMonitor)
    {
        Guard.NotNullOrWhiteSpace(connectionName);
        Guard.NotNull(optionsMonitor);

        _connectionName = connectionName;
        _optionsMonitor = optionsMonitor;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.Get(_connectionName);
        if (options.Endpoint is null)
            return HealthCheckResult.Unhealthy($"BitNet endpoint '{_connectionName}' is not configured.");

        using var http = new HttpClient { Timeout = options.HealthProbeTimeout };
        using var timeoutCts = new CancellationTokenSource(options.HealthProbeTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        try
        {
            using var response = await http.GetAsync(new Uri(options.Endpoint, "/health"), linkedCts.Token)
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy($"BitNet '{_connectionName}' responded {(int)response.StatusCode}.")
                : HealthCheckResult.Unhealthy($"BitNet '{_connectionName}' responded {(int)response.StatusCode}.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Host-driven cancellation (shutdown, request abort): re-throw so the
            // diagnostics pipeline propagates instead of masking the signal as Unhealthy.
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            // Probe-timeout or transport failure: map to Unhealthy so the check stays graceful.
            return HealthCheckResult.Unhealthy($"BitNet '{_connectionName}' probe failed.", ex);
        }
    }
}
