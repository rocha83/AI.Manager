using Rochas.AI.Manager.Domain.Harness;

namespace Rochas.AI.Manager.Bridge;

/// <summary>
/// Monta as opções do motor a partir dos enunciados que vieram do banco.
/// Texto vazio/nulo cai no default embutido — o dado sempre vence.
/// </summary>
public static class BridgeOptionsFactory
{
    public static BridgeOptions FromContent(HarnessContent? content, BridgeOptions? baseline = null)
    {
        var options = baseline ?? new BridgeOptions();
        if (!string.IsNullOrWhiteSpace(content?.Behavior)) options.BehaviorTail = content!.Behavior!;
        if (!string.IsNullOrWhiteSpace(content?.ToolsUsage)) options.ToolsTail = content!.ToolsUsage!;
        return options;
    }

    /// <summary>
    /// Opções a partir do CADASTRO do modelo (ModelBinding): é lá que fica o
    /// "Utiliza coerção de tools" dos modelos legados, além de temperatura,
    /// thinking, teto de saída e janela de contexto.
    /// </summary>
    public static BridgeOptions FromModel(ModelBinding model, HarnessContent? content = null, BridgeOptions? baseline = null)
    {
        var options = FromContent(content, baseline);
        options.Thinking = string.IsNullOrWhiteSpace(model.Thinking) ? options.Thinking : model.Thinking;
        options.Temperature = model.Temperature;
        options.MaxTokens = model.MaxTokens;
        options.ContextTokens = model.ContextWindow;
        options.CoerceNarrated = model.UsesToolCoercion;
        return options;
    }
}
