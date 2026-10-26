namespace Rochas.AI.Manager.Bridge.Contracts;

/// <summary>Resultado da validacao de manifesto: erros bloqueiam execucao.</summary>
public sealed record BridgeValidationResult(
    bool IsValid,
    IReadOnlyList<BridgeValidationIssue> Errors,
    IReadOnlyList<BridgeValidationIssue> Warnings)
{
    public static BridgeValidationResult Valid(params BridgeValidationIssue[] warnings) =>
        new(true, Array.Empty<BridgeValidationIssue>(), warnings);

    public static BridgeValidationResult Invalid(params BridgeValidationIssue[] errors) =>
        new(false, errors, Array.Empty<BridgeValidationIssue>());
}

/// <summary>Problema localizado no manifesto (path JSON + codigo estavel).</summary>
public sealed record BridgeValidationIssue(string Code, string Path, string Message);

/// <summary>Comandos resolvidos pelo dry-run, sem executar nada.</summary>
public sealed record BridgeManifestDryRun(
    string SchemaVersion,
    string? ManifestHash,
    string? ManifestHashComputed,
    bool ManifestHashMatches,
    IReadOnlyList<BridgeDryRunStep> Steps,
    IReadOnlyList<BridgeValidationIssue> Issues);

/// <summary>Um comando resolvido do manifesto.</summary>
public sealed record BridgeDryRunStep(
    string StageKey,
    string StepKey,
    string Kind,
    string Phase,
    string CommandLine,
    string? WorkingDirectory,
    int TimeoutSec,
    IReadOnlyList<string> DependsOn);
