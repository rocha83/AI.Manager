using System.ComponentModel.DataAnnotations.Schema;
using Rochas.AI.Manager.Domain.Entities.Base;

namespace Rochas.AI.Manager.Domain.TestBench;

/// <summary>
/// Plano de teste versionado (dono do manifesto imutavel). O plano nasce
/// <c>Draft</c>; so vira <c>Approved</c> por acao humana. O executor nunca le o
/// banco: recebe o manifesto pronto.
/// </summary>
[Table("experiment_plans")]
public sealed class ExperimentPlan : BaseEntity
{
    [Column("experiment_id")]
    public int ExperimentId { get; set; }

    [Column("source_commit")]
    public string SourceCommit { get; set; } = string.Empty;

    [Column("manifest_hash")]
    public string ManifestHash { get; set; } = string.Empty;

    [Column("manifest_json")]
    public string ManifestJson { get; set; } = string.Empty;

    [Column("status")]
    public string Status { get; set; } = "Draft";

    [Column("statement")]
    public string? Statement { get; set; }

    [Column("target")]
    public string Target { get; set; } = "q4";
}

/// <summary>Stage do circuito (agrupador ordenado de steps com politica de falha).</summary>
[Table("circuit_stages")]
public sealed class CircuitStage : BaseEntity
{
    [Column("plan_id")]
    public int PlanId { get; set; }

    [Column("key")]
    public string Key { get; set; } = string.Empty;

    [Column("title")]
    public string? Title { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("max_parallel_steps")]
    public int MaxParallelSteps { get; set; } = 1;

    [Column("on_failure")]
    public string OnFailure { get; set; } = "continue";

    [Column("gate_policy_json")]
    public string? GatePolicyJson { get; set; }
}

/// <summary>Step do circuito: comando do host ou cenario <c>native</c>.</summary>
[Table("circuit_steps")]
public sealed class CircuitStep : BaseEntity
{
    [Column("stage_id")]
    public int StageId { get; set; }

    [Column("key")]
    public string Key { get; set; } = string.Empty;

    [Column("kind")]
    public string Kind { get; set; } = "native";

    [Column("command")]
    public string? Command { get; set; }

    [Column("args_json")]
    public string? ArgsJson { get; set; }

    [Column("env_json")]
    public string? EnvJson { get; set; }

    [Column("timeout_sec")]
    public int TimeoutSec { get; set; }

    [Column("depends_on_json")]
    public string? DependsOnJson { get; set; }

    [Column("scenario_selector_json")]
    public string? ScenarioSelectorJson { get; set; }

    [Column("verdict_parser")]
    public string? VerdictParser { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }
}
