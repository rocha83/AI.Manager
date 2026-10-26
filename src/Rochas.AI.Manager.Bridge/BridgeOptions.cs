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
    /// Sonda de repetição semântica (mesma ideia/pergunta reformulada, sem
    /// repetir bytes). O legado só tinha janela de igualdade/similaridade; por
    /// isso a sonda é opt-in e fica isolada por cliente.
    /// </summary>
    public bool AntiRepeatSemantic { get; set; }

    /// <summary>
    /// Liga a sonda semântica no cliente opencode (User-Agent), onde o loop de
    /// conteúdo bloqueava o WAR. Outros clientes preservam a paridade do legado;
    /// <see cref="AntiRepeatSemantic"/> liga para todos.
    /// </summary>
    public bool AntiRepeatSemanticForOpencode { get; set; } = true;

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

    /// <summary>
    /// Substitui o system do cliente pelo <see cref="MainHarness"/> no caminho
    /// gerido. O gate e o CLIENTE (<see cref="OpencodeClient"/>), nunca o sufixo
    /// do modelo; as caudas coexistem com a base.
    /// </summary>
    public bool MainHarness { get; set; }

    /// <summary>
    /// Model id carrega sufixo de role. Preservado para diagnostico/cadastro;
    /// NAO bloqueia mais o main harness (o WAR usa &lt;modelo&gt;-build).
    /// </summary>
    public bool HasProfile { get; set; }

    /// <summary>
    /// Cliente da requisicao e o CLI do opencode (User-Agent): ele traz o
    /// proprio harness, entao base e tails ficam de fora no escopo "managed".
    /// </summary>
    public bool OpencodeClient { get; set; }

    /// <summary>
    /// Onde as caudas valem: "managed" (default; todos menos o CLI do opencode),
    /// "all" (tambem no opencode) ou "off" (nenhuma).
    /// </summary>
    public string TailScope { get; set; } = "managed";

    /// <summary>
    /// Harness legado: strip de call narrada no historico + guardrail textual.
    /// Desligado por padrao no componente novo (o shim/canal nativo cobre o caso).
    /// </summary>
    public bool LegacyHarness { get; set; }

    /// <summary>
    /// Modo QwenStandard: ModelPrompt sintetizado + traducao de nomes/args
    /// OpenAI &lt;-&gt; Alibaba nos dois sentidos.
    /// </summary>
    public bool QwenStandard { get; set; }

    /// <summary>
    /// Estrategia de tools: remove a tool <c>execute</c> (Code Mode) e troca o
    /// bloco nativo pelo aviso de tools diretas. Default ligado.
    /// </summary>
    public bool ToolStrategy { get; set; } = true;

    /// <summary>
    /// Falhas consecutivas no mesmo alvo que injetam o aviso no ultimo resultado
    /// de tool (0 = desliga o nudge).
    /// </summary>
    public int BreakerSoft { get; set; } = 3;

    /// <summary>
    /// Falhas consecutivas que encerram o turno sem chamar o modelo
    /// (0 = desliga o curto-circuito).
    /// </summary>
    public int BreakerHard { get; set; } = 6;

    /// <summary>Copia top_p/top_k/repeat_penalty/... do request para o corpo chat.</summary>
    public bool SamplingPassthrough { get; set; }

    /// <summary>Guard rail textual do harness legado (anti-loop).</summary>
    public string? SystemGuardrail { get; set; }

    /// <summary>Extrai tool call do texto quando o modelo narra em vez de emitir nativo.</summary>
    public bool CoerceNarrated { get; set; } = true;

    /// <summary>Guarda de contexto (413 antes de chamar o upstream).</summary>
    public bool ContextGuard { get; set; }

    public double ContextThreshold { get; set; } = 0.95;

    public int ContextTokens { get; set; }

    /// <summary>
    /// "json" = shim generico de tool calling (remove tools, injeta envelope no
    /// system e forca grammar JSON). Vazio = desligado (default).
    /// </summary>
    public string ToolShimMode { get; set; } = string.Empty;

    /// <summary>Cap de chars por string no grammar do shim; 0 = sem cap.</summary>
    public int ToolShimMaxString { get; set; } = 2000;

    /// <summary>Calls minimas no historico antes de liberar {"final":...}; 0 = libera sempre.</summary>
    public int ToolShimMinCalls { get; set; }

    /// <summary>
    /// Copia as opcoes para o pedido corrente e marca o cliente pelo User-Agent.
    /// O host e singleton; mutar a instancia compartilhada criaria corrida entre
    /// requisicoes concorrentes.
    /// </summary>
    /// <param name="userAgent">User-Agent do pedido (pode ser nulo).</param>
    /// <returns>Copia isolada com <see cref="OpencodeClient"/> resolvido.</returns>
    public BridgeOptions ForClient(string? userAgent)
    {
        var copy = (BridgeOptions)MemberwiseClone();
        copy.OpencodeClient = !string.IsNullOrWhiteSpace(userAgent)
            && userAgent.Contains("opencode", StringComparison.OrdinalIgnoreCase);
        copy.AntiRepeatSemantic = copy.AntiRepeatSemantic
            || (copy.AntiRepeatSemanticForOpencode && copy.OpencodeClient);
        return copy;
    }
}
