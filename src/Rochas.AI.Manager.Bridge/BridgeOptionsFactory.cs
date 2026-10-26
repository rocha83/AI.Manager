using Rochas.AI.Manager.Bridge.Contracts;

namespace Rochas.AI.Manager.Bridge;

/// <summary>
/// Monta as opcoes do motor a partir dos enunciados que vieram do banco.
/// Texto vazio/nulo cai no default embutido — o dado sempre vence.
/// </summary>
public static class BridgeOptionsFactory
{
    public static BridgeOptions FromContent(BridgeHarnessContent? content, BridgeOptions? baseline = null)
    {
        var options = baseline ?? new BridgeOptions();
        if (!string.IsNullOrWhiteSpace(content?.Behavior)) options.BehaviorTail = content!.Behavior!;
        if (!string.IsNullOrWhiteSpace(content?.ToolsUsage)) options.ToolsTail = content!.ToolsUsage!;
        return options;
    }

    /// <summary>
    /// Opcoes a partir do CADASTRO do modelo (ModelBinding): é lá que fica o
    /// "Utiliza coerção de tools" dos modelos legados, além de temperatura,
    /// thinking, teto de saída e janela de contexto.
    /// </summary>
    public static BridgeOptions FromModel(
        BridgeModelSettings model,
        BridgeHarnessContent? content = null,
        BridgeOptions? baseline = null)
    {
        var options = FromContent(content, baseline);
        options.Thinking = string.IsNullOrWhiteSpace(model.Thinking) ? options.Thinking : model.Thinking;
        options.Temperature = model.Temperature;
        options.MaxTokens = model.MaxTokens;
        options.ContextTokens = model.ContextWindow;
        switch ((model.ToolMode ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "shim":
                options.ToolShimMode = "json";
                options.CoerceNarrated = false;
                break;
            case "native":
                options.ToolShimMode = string.Empty;
                options.CoerceNarrated = false;
                break;
            case "narrated":
                options.ToolShimMode = string.Empty;
                options.CoerceNarrated = true;
                break;
            default:
                options.CoerceNarrated = model.UsesToolCoercion;
                break;
        }
        options.MainHarness = model.MainHarness;
        options.HasProfile = model.HasProfile;
        options.LegacyHarness = model.LegacyHarness;
        options.QwenStandard = model.QwenStandard;
        options.ToolStrategy = model.ToolStrategy;
        options.BreakerSoft = model.BreakerSoft;
        options.BreakerHard = model.BreakerHard;
        return options;
    }
}
