namespace Rochas.AI.Manager.Bridge.Contracts;

/// <summary>
/// Seed versionado de um baseline de testes: envelope com procedencia
/// (commit/repositorio) + manifesto imutavel + perfis de runner (Q4 gate,
/// Q3 espelho). O manifesto continua sendo a unica fonte executavel; o envelope
/// nao entra no hash.
/// </summary>
public sealed class BridgeBaseline
{
    public string SchemaVersion { get; set; } = BridgeContractVersion.SchemaVersion;
    public string SourceCommit { get; set; } = string.Empty;
    public string? SourceRepository { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public BridgeManifest Manifest { get; set; } = new();
    public List<BridgeRunnerProfile> Runners { get; set; } = new();
}

/// <summary>
/// Perfil de runner que pode executar o manifesto. Q4 e gate; Q3 e espelho e
/// so entra depois do verde no gate (roadmap §F2).
/// </summary>
public sealed class BridgeRunnerProfile
{
    public string Id { get; set; } = "q4";
    public string Name { get; set; } = "Q4 gate";
    public string BridgeUrl { get; set; } = "http://127.0.0.1:4124";
    public string UpstreamUrl { get; set; } = "http://127.0.0.1:4100";
    public string ModelId { get; set; } = string.Empty;
    public int ContextWindow { get; set; }
    public int MaxTokens { get; set; }
    public bool IsGate { get; set; } = true;
    public bool IsMirror { get; set; }
    public List<string> Capabilities { get; set; } = new();
}
