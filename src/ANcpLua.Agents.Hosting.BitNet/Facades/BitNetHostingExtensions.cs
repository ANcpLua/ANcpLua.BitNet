using ANcpLua.Roslyn.Utilities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace ANcpLua.Agents.Hosting.BitNet;

/// <summary>
///     Qyl-prefixed hosting facades for <c>bitnet.cpp</c> <c>llama-server</c>. Registers an
///     <see cref="IChatClient" /> keyed by connection name, named <see cref="BitNetClientOptions" />,
///     and a health check that probes <c>&lt;endpoint&gt;/health</c>.
/// </summary>
public static class BitNetHostingExtensions
{
    /// <summary>Default connection name used by the parameterless overloads.</summary>
    public const string DefaultConnectionName = "bitnet";

    /// <summary>Health-check name prefix; the registered check is <c>bitnet:&lt;connectionName&gt;</c>.</summary>
    public const string HealthCheckNamePrefix = "bitnet:";

    /// <summary>Health-check tag applied to every BitNet probe so consumers can filter by tag.</summary>
    public const string HealthCheckTag = "bitnet";

    /// <summary>
    ///     Registers a keyed BitNet <see cref="IChatClient" /> under the default connection name
    ///     (<c>"bitnet"</c>). Reads <c>BITNET_URL</c> / <c>BITNET_API_PATH</c> / <c>BITNET_MODEL</c>
    ///     from the environment when nothing is bound from configuration.
    /// </summary>
    public static IHostApplicationBuilder AddBitNetChatClient(this IHostApplicationBuilder builder) =>
        builder.AddBitNetChatClient(DefaultConnectionName, configure: null);

    /// <summary>
    ///     Registers a keyed BitNet <see cref="IChatClient" /> under <paramref name="connectionName" />.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="connectionName">DI keyed-service key. Also drives <c>ConnectionStrings:&lt;name&gt;</c> and <c>BitNet:&lt;name&gt;</c> lookups.</param>
    /// <param name="configure">Optional programmatic override applied after configuration but before environment-variable overrides.</param>
    /// <returns>The same host application builder for chaining.</returns>
    /// <remarks>
    ///     <para>Resolution order (later wins):</para>
    ///     <list type="number">
    ///         <item><c>BitNet:&lt;connectionName&gt;</c> configuration section.</item>
    ///         <item><c>ConnectionStrings:&lt;connectionName&gt;</c> parsed as an absolute URI into <see cref="BitNetClientOptions.Endpoint" />.</item>
    ///         <item><paramref name="configure" /> callback (programmatic override).</item>
    ///         <item>Environment variables <c>BITNET_URL</c>, <c>BITNET_API_PATH</c>, <c>BITNET_MODEL</c>.</item>
    ///     </list>
    /// </remarks>
    public static IHostApplicationBuilder AddBitNetChatClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<BitNetClientOptions>? configure = null)
    {
        Guard.NotNull(builder);
        Guard.NotNullOrWhiteSpace(connectionName);

        var options = BuildOptions(builder.Configuration, connectionName, configure);

        builder.Services.AddOptions<BitNetClientOptions>(connectionName)
            .Configure(target => CopyTo(options, target));

        var chatClientBuilder = builder.Services.AddKeyedChatClient(
            serviceKey: connectionName,
            innerClientFactory: _ => BitNetChatClientFactory.Create(options));

        if (options.EnableOpenTelemetry)
            chatClientBuilder.UseOpenTelemetry(sourceName: options.OpenTelemetrySourceName);

        builder.Services.AddHealthChecks()
            .AddTypeActivatedCheck<BitNetHealthCheck>(
                name: HealthCheckNamePrefix + connectionName,
                failureStatus: HealthStatus.Unhealthy,
                tags: [HealthCheckTag, "llm"],
                args: [connectionName]);

        return builder;
    }

    /// <summary>
    ///     Service-collection overload registering BitNet under the default connection name
    ///     (<c>"bitnet"</c>). Reads <c>BITNET_*</c> environment variables — no configuration binding.
    /// </summary>
    public static IServiceCollection AddBitNetChatClient(this IServiceCollection services) =>
        services.AddBitNetChatClient(DefaultConnectionName, configure: null);

    /// <summary>
    ///     Service-collection overload of <see cref="AddBitNetChatClient(IHostApplicationBuilder, string, Action{BitNetClientOptions}?)" />
    ///     for callers without an <see cref="IHostApplicationBuilder" />. Skips configuration binding
    ///     — caller must supply every value via <paramref name="configure" /> or
    ///     <c>BITNET_*</c> environment variables.
    /// </summary>
    public static IServiceCollection AddBitNetChatClient(
        this IServiceCollection services,
        string connectionName,
        Action<BitNetClientOptions>? configure = null)
    {
        Guard.NotNull(services);
        Guard.NotNullOrWhiteSpace(connectionName);

        var options = new BitNetClientOptions();
        configure?.Invoke(options);
        options.ApplyEnvironmentOverrides();
        options.Validate();

        services.AddOptions<BitNetClientOptions>(connectionName)
            .Configure(target => CopyTo(options, target));

        var chatClientBuilder = services.AddKeyedChatClient(
            serviceKey: connectionName,
            innerClientFactory: _ => BitNetChatClientFactory.Create(options));

        if (options.EnableOpenTelemetry)
            chatClientBuilder.UseOpenTelemetry(sourceName: options.OpenTelemetrySourceName);

        services.AddHealthChecks()
            .AddTypeActivatedCheck<BitNetHealthCheck>(
                name: HealthCheckNamePrefix + connectionName,
                failureStatus: HealthStatus.Unhealthy,
                tags: [HealthCheckTag, "llm"],
                args: [connectionName]);

        return services;
    }

    private static BitNetClientOptions BuildOptions(
        IConfiguration configuration,
        string connectionName,
        Action<BitNetClientOptions>? configure)
    {
        var options = new BitNetClientOptions();
        configuration.GetSection("BitNet:" + connectionName).Bind(options);

        var connectionString = configuration.GetConnectionString(connectionName);
        if (!string.IsNullOrWhiteSpace(connectionString) &&
            Uri.TryCreate(connectionString, UriKind.Absolute, out var parsedEndpoint))
        {
            options.Endpoint = parsedEndpoint;
        }

        configure?.Invoke(options);
        options.ApplyEnvironmentOverrides();
        options.Validate();
        return options;
    }

    private static void CopyTo(BitNetClientOptions source, BitNetClientOptions target)
    {
        target.Endpoint = source.Endpoint;
        target.ApiPath = source.ApiPath;
        target.Model = source.Model;
        target.HttpClientTimeout = source.HttpClientTimeout;
        target.HealthProbeTimeout = source.HealthProbeTimeout;
        target.EnableOpenTelemetry = source.EnableOpenTelemetry;
        target.OpenTelemetrySourceName = source.OpenTelemetrySourceName;
    }
}
