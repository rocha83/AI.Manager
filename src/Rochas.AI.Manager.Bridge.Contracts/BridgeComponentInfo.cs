namespace Rochas.AI.Manager.Bridge.Contracts;

/// <summary>
/// Handshake do componente: o cliente compara <see cref="ContractVersion"/> e
/// <see cref="SchemaVersion"/> antes de enviar manifesto ou abrir stream.
/// </summary>
public sealed record BridgeComponentInfo(
    string Name,
    string Version,
    string ContractVersion,
    string SchemaVersion,
    IReadOnlyList<string> SupportedSchemaVersions,
    IReadOnlyList<string> Features);

/// <summary>Estado de saude do host self-hosted.</summary>
public sealed record BridgeStatusResponse(
    string Name,
    string Version,
    string ContractVersion,
    string SchemaVersion,
    DateTimeOffset StartedAtUtc,
    long UptimeSeconds,
    string UpstreamUrl,
    string Model,
    bool IsGate,
    int Port,
    string Listen,
    IReadOnlyDictionary<string, string>? Metadata = null);
