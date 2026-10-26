using System.ComponentModel.DataAnnotations.Schema;
using Rochas.AI.Manager.Domain.Entities.Base;

namespace Rochas.AI.Manager.Domain.TestBench;

/// <summary>
/// Enunciado executavel persistido como DADO (prompt, workspace, tools, loop e
/// assertions em JSON). Nenhuma regra de belt mora em codigo.
/// </summary>
[Table("test_scenarios")]
public sealed class TestScenarioRecord : BaseEntity
{
    [Column("plan_id")]
    public int PlanId { get; set; }

    [Column("key")]
    public string Key { get; set; } = string.Empty;

    [Column("title")]
    public string? Title { get; set; }

    [Column("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [Column("system_prompt")]
    public string? SystemPrompt { get; set; }

    [Column("workspace_json")]
    public string? WorkspaceJson { get; set; }

    [Column("model_json")]
    public string? ModelJson { get; set; }

    [Column("tools_json")]
    public string? ToolsJson { get; set; }

    [Column("loop_json")]
    public string? LoopJson { get; set; }

    [Column("assertions_json")]
    public string? AssertionsJson { get; set; }

    [Column("tags")]
    public string? Tags { get; set; }

    [Column("weight")]
    public int Weight { get; set; } = 1;
}

/// <summary>
/// Perfil de runner (Q4 gate / Q3 espelho). O espelho fica bloqueado ate o
/// gate verde; o executor so aceita <see cref="IsGate"/> para promover release.
/// </summary>
[Table("runner_profiles")]
public sealed class RunnerProfile : BaseEntity
{
    [Column("runner_id")]
    public string RunnerId { get; set; } = "q4";

    [Column("bridge_url")]
    public string BridgeUrl { get; set; } = "http://127.0.0.1:4124";

    [Column("upstream_url")]
    public string UpstreamUrl { get; set; } = "http://127.0.0.1:4100";

    [Column("model_id")]
    public string ModelId { get; set; } = string.Empty;

    [Column("context_window")]
    public int ContextWindow { get; set; }

    [Column("max_tokens")]
    public int MaxTokens { get; set; }

    [Column("is_gate")]
    public bool IsGate { get; set; } = true;

    [Column("is_mirror")]
    public bool IsMirror { get; set; }

    [Column("capabilities_json")]
    public string? CapabilitiesJson { get; set; }
}
