using System.Text.Json.Nodes;

namespace Rochas.AI.Manager.Bridge.Transport;

/// <summary>Configuracao da guarda de contexto lida do host.</summary>
public readonly record struct ContextGuardSettings(bool Enabled, double Threshold, int Tokens);

/// <summary>
/// Guarda de contexto: detecta, na resposta do upstream, que o prompt chegou ao
/// limiar do ctx do modelo. Regra do nucleo: o host so informa os parametros e
/// devolve 413 pedindo compactacao, sem deixar o upstream estourar.
/// </summary>
public static class ContextGuard
{
    public static bool Tripped(string json, ContextGuardSettings settings, out int promptTokens, out double fraction)
    {
        promptTokens = 0;
        fraction = 0;
        if (!settings.Enabled || settings.Tokens <= 0) return false;
        try
        {
            promptTokens = JsonNode.Parse(json)?["usage"]?["prompt_tokens"]?.GetValue<int>() ?? 0;
        }
        catch
        {
            return false;
        }

        fraction = (double)promptTokens / settings.Tokens;
        return fraction >= settings.Threshold;
    }

    /// <summary>Corpo 413 no formato OpenAI, com o detalhe que o cliente usa para compactar.</summary>
    public static JsonObject BuildPayload(int promptTokens, double fraction, int contextTokens) => new()
    {
        ["error"] = new JsonObject
        {
            ["message"] = $"Contexto em {fraction:P0} ({promptTokens} tokens de {contextTokens}). Compacte a sessao e tente de novo.",
            ["type"] = "invalid_request_error",
            ["code"] = "context_compact_required"
        }
    };
}
