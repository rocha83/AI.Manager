using System.ComponentModel.DataAnnotations.Schema;
using Rochas.AI.Manager.Domain.Entities.Base;

namespace Rochas.AI.Manager.Domain.TestBench;

/// <summary>Execucao de um plano (gate + contadores + resultado canonico).</summary>
[Table("test_runs")]
public sealed class TestRun : BaseEntity
{
    [Column("plan_id")]
    public int PlanId { get; set; }

    [Column("status")]
    public string Status { get; set; } = "Running";

    [Column("gate")]
    public string Gate { get; set; } = "Pending";

    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    [Column("finished_at")]
    public DateTime? FinishedAt { get; set; }

    [Column("result_json")]
    public string? ResultJson { get; set; }

    [Column("evidence_hash")]
    public string? EvidenceHash { get; set; }
}

/// <summary>Resultado por stage de uma execucao.</summary>
[Table("test_stage_runs")]
public sealed class TestStageRun : BaseEntity
{
    [Column("run_id")]
    public int RunId { get; set; }

    [Column("stage_key")]
    public string StageKey { get; set; } = string.Empty;

    [Column("verdict")]
    public string Verdict { get; set; } = "SKIP";

    [Column("seconds")]
    public double Seconds { get; set; }

    [Column("cause")]
    public string? Cause { get; set; }
}

/// <summary>Resultado por step de uma execucao.</summary>
[Table("test_step_runs")]
public sealed class TestStepRun : BaseEntity
{
    [Column("run_id")]
    public int RunId { get; set; }

    [Column("stage_key")]
    public string StageKey { get; set; } = string.Empty;

    [Column("step_key")]
    public string StepKey { get; set; } = string.Empty;

    [Column("verdict")]
    public string Verdict { get; set; } = "SKIP";

    [Column("seconds")]
    public double Seconds { get; set; }

    [Column("exit_code")]
    public int? ExitCode { get; set; }

    [Column("cause")]
    public string? Cause { get; set; }
}

/// <summary>Metricas por cenario (arquivos, chars, duracao).</summary>
[Table("test_scenario_runs")]
public sealed class TestScenarioRun : BaseEntity
{
    [Column("run_id")]
    public int RunId { get; set; }

    [Column("scenario_key")]
    public string ScenarioKey { get; set; } = string.Empty;

    [Column("verdict")]
    public string Verdict { get; set; } = "SKIP";

    [Column("seconds")]
    public double Seconds { get; set; }

    [Column("files_created")]
    public int FilesCreated { get; set; }

    [Column("total_chars")]
    public long TotalChars { get; set; }

    [Column("detail")]
    public string? Detail { get; set; }
}

/// <summary>Evidencia imutavel de uma execucao (arquivo + SHA-256).</summary>
[Table("test_evidence")]
public sealed class TestEvidence : BaseEntity
{
    [Column("run_id")]
    public int RunId { get; set; }

    [Column("kind")]
    public string Kind { get; set; } = "log";

    [Column("path")]
    public string Path { get; set; } = string.Empty;

    [Column("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [Column("bytes")]
    public long Bytes { get; set; }
}
