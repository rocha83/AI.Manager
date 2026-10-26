namespace Rochas.AI.Manager.Bridge.Contracts;

/// <summary>
/// Manifesto imutavel de um experimento de teste. O AI.Manager monta o
/// manifesto resolvido; o Bridge valida, executa e devolve resultado. O executor
/// nunca le o banco do AI.Manager.
/// </summary>
public sealed class BridgeManifest
{
    public string SchemaVersion { get; set; } = BridgeContractVersion.SchemaVersion;
    public BridgeExperimentRef Experiment { get; set; } = new();
    public BridgeRunnerRef Runner { get; set; } = new();
    public BridgeTarget Target { get; set; } = new();
    public BridgeHarnessRef? Harness { get; set; }
    public List<BridgeStage> Stages { get; set; } = new();
    public List<BridgeScenario> Scenarios { get; set; } = new();
}

/// <summary>Identidade e versao do experimento dono do manifesto.</summary>
public sealed class BridgeExperimentRef
{
    public long Id { get; set; }
    public int Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Statement { get; set; }
    public string? ManifestHash { get; set; }
}

/// <summary>Executor que pode rodar o manifesto e suas capacidades declaradas.</summary>
public sealed class BridgeRunnerRef
{
    public string Id { get; set; } = string.Empty;
    public List<string> Capabilities { get; set; } = new();
}

/// <summary>Alvo de inferencia (Q4 gate ou Q3 espelho pos-green).</summary>
public sealed class BridgeTarget
{
    public string BridgeUrl { get; set; } = "http://127.0.0.1:4124";
    public string UpstreamUrl { get; set; } = "http://127.0.0.1:4100";
    public string ModelId { get; set; } = string.Empty;
    public int ContextWindow { get; set; }
    public int MaxTokens { get; set; }
    public bool IsGate { get; set; } = true;
}

/// <summary>Snapshot da versao de harness aplicada no manifesto.</summary>
public sealed class BridgeHarnessRef
{
    public int ProfileVersionId { get; set; }
    public string? Checksum { get; set; }
    public string Mode { get; set; } = "BridgeManaged";
}

public sealed class BridgeStage
{
    public string Key { get; set; } = string.Empty;
    public string? Title { get; set; }
    public int Order { get; set; }
    public int MaxParallelSteps { get; set; } = 1;
    public BridgeGatePolicy Gate { get; set; } = new();
    public string OnFailure { get; set; } = "continue";
    public List<BridgeBuildSpec> Builds { get; set; } = new();
    public List<BridgePreflightCheck> Preflight { get; set; } = new();
    public List<BridgeStep> Steps { get; set; } = new();
}

public sealed class BridgeGatePolicy
{
    public string Policy { get; set; } = "default";
    public List<string> AllowedCauses { get; set; } = new();
}

public sealed class BridgeBuildSpec
{
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public List<string> Args { get; set; } = new();
    public List<string> Inputs { get; set; } = new();
    public List<string> Outputs { get; set; } = new();
    public Dictionary<string, string> Env { get; set; } = new();
    public int TimeoutSec { get; set; }
}

public sealed class BridgePreflightCheck
{
    /// <summary>http-json, bridge-status, command, file-exists ou port-open.</summary>
    public string Kind { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Command { get; set; }
    public List<string> Args { get; set; } = new();
    public string? Path { get; set; }
    public int TimeoutSec { get; set; } = 30;
}

public sealed class BridgeStep
{
    public string Key { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string? Command { get; set; }
    public List<string> Args { get; set; } = new();
    public Dictionary<string, string> Env { get; set; } = new();
    public int TimeoutSec { get; set; }
    public BridgeRetryPolicy? Retry { get; set; }
    public List<string> DependsOn { get; set; } = new();
    public string? When { get; set; }
    public List<string> NextSteps { get; set; } = new();
    public string? VerdictParser { get; set; }
    public BridgeTestSelector? ScenarioSelector { get; set; }
}

public sealed class BridgeRetryPolicy
{
    public int MaxAttempts { get; set; } = 1;
    public List<string> RetryOn { get; set; } = new();
}

public sealed class BridgeTestSelector
{
    public string Suite { get; set; } = string.Empty;
    public List<string> Cases { get; set; } = new();
}

/// <summary>Enunciado executavel: prompt, workspace, ferramentas, loop e assertions.</summary>
public sealed class BridgeScenario
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string? SystemPrompt { get; set; }
    public BridgeScenarioWorkspace Workspace { get; set; } = new();
    public BridgeScenarioModel Model { get; set; } = new();
    public BridgeScenarioTools Tools { get; set; } = new();
    public BridgeScenarioLoop Loop { get; set; } = new();
    public List<BridgeAssertion> Assertions { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public int Weight { get; set; } = 1;
}

public sealed class BridgeScenarioWorkspace
{
    public string Template { get; set; } = string.Empty;
    public string? Path { get; set; }
    public bool Clean { get; set; } = true;
}

public sealed class BridgeScenarioModel
{
    public string Role { get; set; } = "build";
    public double? Temperature { get; set; }
    public string? Thinking { get; set; }
    public int? MaxTokens { get; set; }
}

public sealed class BridgeScenarioTools
{
    /// <summary>required, auto, none.</summary>
    public string Mode { get; set; } = "auto";
    public List<string> Allow { get; set; } = new();
    public string? ToolChoice { get; set; }
}

public sealed class BridgeScenarioLoop
{
    public int MaxTurns { get; set; } = 10;
    public int MaxNudges { get; set; } = 2;
    public bool Review { get; set; } = true;
    public int StallLimit { get; set; }
}

public sealed class BridgeAssertion
{
    public string Type { get; set; } = string.Empty;
    public string? Path { get; set; }
    public string? Ext { get; set; }
    public string? Expected { get; set; }
    public int? Value { get; set; }
    public bool Required { get; set; } = true;
}
