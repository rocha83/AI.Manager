namespace Rochas.AI.Manager.Bridge.Contracts;

/// <summary>
/// Resultado de uma execucao de manifesto. O gate e calculado pelo Bridge e
/// armazenado sem recalculo; Q3 nunca altera este objeto.
/// </summary>
public sealed class BridgeResult
{
    public string RunId { get; set; } = string.Empty;
    public string ManifestHash { get; set; } = string.Empty;
    public string Status { get; set; } = "Queued";
    public BridgeGateResult Gate { get; set; } = new();
    public List<BridgeStageResult> Stages { get; set; } = new();
    public BridgeEnvironment Environment { get; set; } = new();
    public List<BridgeArtifact> Artifacts { get; set; } = new();
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
}

public sealed class BridgeGateResult
{
    public string Policy { get; set; } = "default";
    public int Pass { get; set; }
    public int Fail { get; set; }
    public int Skip { get; set; }
    public int Stale { get; set; }
    public bool Green { get; set; }
}

public sealed class BridgeStageResult
{
    public string Key { get; set; } = string.Empty;
    public string Status { get; set; } = "Queued";
    public BridgeGateResult Gate { get; set; } = new();
    public long DurationMs { get; set; }
    public List<BridgeStepResult> Steps { get; set; } = new();
    public List<BridgeScenarioResult> Scenarios { get; set; } = new();
}

public sealed class BridgeStepResult
{
    public string Key { get; set; } = string.Empty;
    public string Status { get; set; } = "Queued";
    public long DurationMs { get; set; }
    public string? Cause { get; set; }
    public int ExitCode { get; set; }
    public string? ArtifactRoot { get; set; }
}

public sealed class BridgeScenarioResult
{
    public string Key { get; set; } = string.Empty;
    public string Status { get; set; } = "Queued";
    public BridgeScenarioMetrics Metrics { get; set; } = new();
    public List<BridgeAssertionResult> Assertions { get; set; } = new();
    public long DurationMs { get; set; }
}

public sealed class BridgeScenarioMetrics
{
    public int Files { get; set; }
    public int Chars { get; set; }
    public int ToolCalls { get; set; }
    public int TokensOut { get; set; }
    public double TokensPerSecond { get; set; }
}

public sealed class BridgeAssertionResult
{
    public string Type { get; set; } = string.Empty;
    public string? Expected { get; set; }
    public string? Actual { get; set; }
    public bool Passed { get; set; }
}

public sealed class BridgeEnvironment
{
    public string RunnerVersion { get; set; } = string.Empty;
    public string ContractVersion { get; set; } = BridgeContractVersion.ContractVersion;
    public string BridgeSha { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int ContextWindow { get; set; }
    public bool IsGate { get; set; }
}

public sealed class BridgeArtifact
{
    /// <summary>events, summary, capture, log, other.</summary>
    public string Kind { get; set; } = "other";
    public string Path { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}
