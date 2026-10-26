using System.Text.Json.Nodes;

namespace Rochas.AI.Manager.Bridge;

/// <summary>
/// Regra de temperatura do tunel, num if: <b>o cliente manda</b> &gt; perfil do
/// role &gt; global 0.3. Nao existe inversao: quem esta no tunel define o valor;
/// o perfil so preenche quando o corpo nao traz temperatura.
/// </summary>
public static class Sampling
{
    /// <summary>Parametros de amostragem que o llama aceita por request.</summary>
    public static readonly string[] PassthroughKeys =
    {
        "top_p", "top_k", "min_p", "repeat_penalty", "presence_penalty",
        "frequency_penalty", "seed", "typical_p", "tfs_z"
    };

    /// <summary>Resolve a temperatura efetiva e a procedencia.</summary>
    /// <param name="requestTemp">Temperatura enviada pelo cliente (null = nao enviou).</param>
    /// <param name="hasProfile">Model id carrega sufixo de role.</param>
    /// <param name="profileTemp">Temperatura do perfil (NaN = perfil sem temperatura).</param>
    /// <param name="globalTemp">Temperatura base da instancia.</param>
    /// <returns>Valor e procedencia ("request" | "profile" | "global").</returns>
    public static (double Value, string Source) Temperature(
        double? requestTemp, bool hasProfile, double profileTemp, double globalTemp)
    {
        if (requestTemp.HasValue) return (requestTemp.Value, "request");
        if (hasProfile && !double.IsNaN(profileTemp)) return (profileTemp, "profile");
        return (globalTemp, "global");
    }

    /// <summary>
    /// Copia os parametros de amostragem do request Responses para o corpo chat
    /// quando o modo passthrough esta ligado. O valor do cliente sempre vence.
    /// </summary>
    /// <param name="source">Request original.</param>
    /// <param name="target">Corpo chat mutado.</param>
    /// <returns>Quantidade de parametros copiados.</returns>
    public static int CopyPassthrough(JsonObject? source, JsonObject? target)
    {
        if (source is null || target is null) return 0;
        int copied = 0;
        foreach (string key in PassthroughKeys)
        {
            if (source[key] is not JsonNode value) continue;
            target[key] = value.DeepClone();
            copied++;
        }
        return copied;
    }
}
