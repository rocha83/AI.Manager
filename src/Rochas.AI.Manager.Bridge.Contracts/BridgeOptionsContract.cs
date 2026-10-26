namespace Rochas.AI.Manager.Bridge.Contracts;

/// <summary>
/// Espelho serializavel das opcoes do motor. Existe para que consumidores
/// (AI.Manager, CLI, testes) nao precisem referenciar a implementacao do motor
/// para montar um perfil de execucao.
/// </summary>
public sealed class BridgeOptionsContract
{
    public string Thinking { get; set; } = "events";
    public double Temperature { get; set; } = 0.5;
    public bool StreamPerDelta { get; set; } = true;
    public bool AntiRepeat { get; set; } = true;
    public bool AntiRepeatRetry { get; set; } = true;
    public string? AntiRepeatNotice { get; set; }
    public int AntiRepeatWindow { get; set; } = 256;
    public int MaxTokens { get; set; }
    public int ToolOutputLimit { get; set; }
    public bool Harness { get; set; } = true;
    public string? ToolsTail { get; set; }
    public string? BehaviorTail { get; set; }
    public string? SystemGuardrail { get; set; }
    public bool CoerceNarrated { get; set; } = true;
    public bool ContextGuard { get; set; }
    public double ContextThreshold { get; set; } = 0.95;
    public int ContextTokens { get; set; }
    public string ToolShimMode { get; set; } = string.Empty;
    public int ToolShimMaxString { get; set; } = 2000;
    public int ToolShimMinCalls { get; set; }
}

/// <summary>
/// Enunciados de harness que o consumidor envia como DADO (nao como codigo).
/// Texto nulo/vazio mantem o default embutido do motor.
/// </summary>
public sealed record BridgeHarnessContent(string? Behavior, string? ToolsUsage)
{
    public static BridgeHarnessContent Empty { get; } = new(null, null);
}

/// <summary>Recorte do cadastro do modelo necessario para montar as opcoes.</summary>
public sealed record BridgeModelSettings(
    string Thinking,
    double Temperature,
    int MaxTokens,
    int ContextWindow,
    bool UsesToolCoercion)
{
    /// <summary>
    /// Modo de tool calling do modelo: "native" (tool_calls), "narrated" (texto
    /// parseado por CoerceNarrated) ou "shim" (envelope JSON + grammar). Vazio =
    /// decide por <see cref="UsesToolCoercion"/>.
    /// </summary>
    public string ToolMode { get; init; } = string.Empty;

    /// <summary>Substitui o system do cliente pelo MainHarness (caminho gerido).</summary>
    public bool MainHarness { get; init; }

    /// <summary>Model id tem sufixo de role: preserva o system do cliente.</summary>
    public bool HasProfile { get; init; }

    /// <summary>Harness legado: strip de call narrada + guardrail textual.</summary>
    public bool LegacyHarness { get; init; }

    /// <summary>Modo QwenStandard (ModelPrompt + traducao OpenAI/Alibaba).</summary>
    public bool QwenStandard { get; init; }

    /// <summary>Estrategia de tools (remove execute/Code Mode).</summary>
    public bool ToolStrategy { get; init; } = true;

    /// <summary>Falhas consecutivas para o nudge do breaker (0 desliga).</summary>
    public int BreakerSoft { get; init; } = 3;

    /// <summary>Falhas consecutivas para o trip do breaker (0 desliga).</summary>
    public int BreakerHard { get; init; } = 6;

    public static BridgeModelSettings Default { get; } =
        new("events", 0.5, 0, 32768, false);
}
