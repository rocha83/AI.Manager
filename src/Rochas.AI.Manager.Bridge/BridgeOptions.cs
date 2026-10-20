namespace Rochas.AI.Manager.Bridge;

/// <summary>
/// Opções do motor de protocolo. Cada valor corresponde a um flag/ajuste que a
/// bridge antiga recebia por CLI/appsettings; aqui vêm do binding do agente.
/// </summary>
public sealed class BridgeOptions
{
    /// <summary>"events" = raciocínio vira item reasoning; "off" = vira texto.</summary>
    public string Thinking { get; set; } = "events";

    public double Temperature { get; set; } = 0.5;

    /// <summary>
    /// Emite o content por delta (efeito "digitando"). Desligado volta a agregar
    /// até 256 chars — comportamento antigo, útil quando o cliente re-renderiza
    /// demais a cada evento.
    /// </summary>
    public bool StreamPerDelta { get; set; } = true;

    /// <summary>Liga a sonda/análise de repetição no raciocínio e no content.</summary>
    public bool AntiRepeat { get; set; } = true;

    /// <summary>Permite um retry com o aviso de contenção quando a repetição dispara.</summary>
    public bool AntiRepeatRetry { get; set; } = true;

    /// <summary>
    /// Texto do aviso de contenção. É ENUNCIADO: vem do anexo "AntiRepeat" do
    /// perfil; o default do motor só vale quando não há dado.
    /// </summary>
    public string? AntiRepeatNotice { get; set; }

    /// <summary>Tamanho (chars) da cauda comparada com o histórico do stream.</summary>
    public int AntiRepeatWindow { get; set; } = 256;

    /// <summary>Teto de saída; 0 = sem teto (quem decide é o EOS/upstream).</summary>
    public int MaxTokens { get; set; }

    /// <summary>Corte por resultado de tool; 0 = sem corte.</summary>
    public int ToolOutputLimit { get; set; }

    /// <summary>Aplica as caudas de harness no system (workspace/formato + tools).</summary>
    public bool Harness { get; set; } = true;

    public string ToolsTail { get; set; } = HarnessTails.Tools;

    public string BehaviorTail { get; set; } = HarnessTails.Behavior;

    /// <summary>Guard rail textual do harness legado (anti-loop).</summary>
    public string? SystemGuardrail { get; set; }

    /// <summary>Extrai tool call do texto quando o modelo narra em vez de emitir nativo.</summary>
    public bool CoerceNarrated { get; set; } = true;

    /// <summary>Guarda de contexto (413 antes de chamar o upstream).</summary>
    public bool ContextGuard { get; set; }

    public double ContextThreshold { get; set; } = 0.95;

    public int ContextTokens { get; set; }
}
