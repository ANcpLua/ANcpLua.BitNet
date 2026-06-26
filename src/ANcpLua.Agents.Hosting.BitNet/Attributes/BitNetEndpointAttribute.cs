namespace ANcpLua.Agents.Hosting.BitNet;

/// <summary>
///     Declares a BitNet endpoint at compile time. Multiple instances may be applied to the same
///     assembly. The bundled Roslyn generator <c>ANcpLua.Agents.Hosting.BitNet.Generators</c>
///     scans these attributes and emits
///     <c>BitNetDiscoveredEndpointsExtensions.AddDiscoveredBitNetClients</c> which calls
///     <see cref="BitNetHostingExtensions.AddBitNetChatClient(Microsoft.Extensions.Hosting.IHostApplicationBuilder, string, System.Action{BitNetClientOptions}?)" />
///     once per declared endpoint.
/// </summary>
/// <remarks>
///     <para>Example:</para>
///     <code>
///     [assembly: BitNetEndpoint("bitnet", "http://localhost:8080", Model = "bitnet-b1.58-2B-4T")]
///     [assembly: BitNetEndpoint("staging", "http://bitnet.staging.internal:8080")]
///     </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class BitNetEndpointAttribute : Attribute
{
    /// <summary>
    ///     Declares a discoverable BitNet endpoint.
    /// </summary>
    /// <param name="connectionName">Logical name; used as the DI keyed-service key.</param>
    /// <param name="endpoint">Absolute endpoint URI of the <c>llama-server</c> process.</param>
    public BitNetEndpointAttribute(string connectionName, string endpoint)
    {
        ConnectionName = connectionName;
        Endpoint = endpoint;
    }

    /// <summary>Logical name; used as the DI keyed-service key.</summary>
    public string ConnectionName { get; }

    /// <summary>Absolute endpoint URI of the <c>llama-server</c> process.</summary>
    public string Endpoint { get; }

    /// <summary>Optional model identifier. Defaults to <see cref="BitNetClientOptions.DefaultModel" />.</summary>
    public string? Model { get; set; }

    /// <summary>Optional API path. Defaults to <see cref="BitNetClientOptions.DefaultApiPath" />.</summary>
    public string? ApiPath { get; set; }

    /// <summary>Whether to register the <c>Microsoft.Extensions.AI</c> OpenTelemetry decorator. Default <see langword="true" />.</summary>
    public bool EnableOpenTelemetry { get; set; } = true;

    /// <summary>Optional OpenTelemetry source-name override.</summary>
    public string? OpenTelemetrySourceName { get; set; }
}
