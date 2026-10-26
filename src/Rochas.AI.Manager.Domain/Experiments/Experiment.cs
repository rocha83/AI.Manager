using System.ComponentModel.DataAnnotations.Schema;
using Rochas.AI.Manager.Domain.Entities.Base;

namespace Rochas.AI.Manager.Domain.Experiments;

[Table("experiments")]
public sealed class Experiment : BaseEntity
{
    [Column("hypothesis")]
    public string? Hypothesis { get; set; }
    [Column("base_artifact_id")]
    public int? BaseArtifactId { get; set; }
    [Column("tuned_artifact_id")]
    public int? TunedArtifactId { get; set; }
    [Column("metrics_json")]
    public string? MetricsJson { get; set; }
    [Column("status")]
    public string Status { get; set; } = "Planned";
    [Column("rejection_reason")]
    public string? RejectionReason { get; set; }

    /// <summary>Tipo do experimento: <c>Training</c> (SFT/DPO/RM) ou <c>Test</c> (WAR).</summary>
    [Column("kind")]
    public string Kind { get; set; } = "Training";

    /// <summary>Enunciado do experimento de teste (o mesmo do manifesto).</summary>
    [Column("statement")]
    public string? Statement { get; set; }
}
