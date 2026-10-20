namespace Rochas.AI.Manager.Bridge;

/// <summary>
/// Sonda de repetição: compara a cauda do texto já emitido com o histórico do
/// próprio stream. O llama não guarda o texto gerado (só n_tokens), então a
/// única prova de loop vem daqui.
/// </summary>
public static class AntiRepeat
{
    public const string DefaultNotice =
        "Aqui e a ponte de contencao OpenCodeBridge: pare de enviar o mesmo raciocinio, provavelmente voce entrou em repeticao invalida; " +
        "quando informada a repeticao, nao responda mais nada, apenas encerre em silencio, obrigado.";

    /// <summary>Posição (chars) onde a cauda repetida começa no texto normalizado, ou -1.</summary>
    public static int DuplicateAt(string? accumulated, int window)
    {
        if (string.IsNullOrEmpty(accumulated) || window < 32) return -1;
        string norm = JsonOps.Normalize(accumulated);
        if (norm.Length >= 2 * window)
        {
            string tail = norm[^window..];
            int at = norm.AsSpan(0, norm.Length - window).IndexOf(tail.AsSpan(), StringComparison.Ordinal);
            if (at >= 0) return at;
        }
        foreach (int unit in new[] { 24, 32, 40, 48, 64, 80, 96, 128 })
        {
            if (norm.Length < 3 * unit) continue;
            var a = norm.AsSpan(norm.Length - unit);
            var b = norm.AsSpan(norm.Length - 2 * unit, unit);
            var c = norm.AsSpan(norm.Length - 3 * unit, unit);
            if (BlockSimilarity(a, b) >= 0.85 && BlockSimilarity(b, c) >= 0.85)
                return norm.Length - 3 * unit;
        }
        return -1;
    }

    public static bool Repeats(string? accumulated, int window) => DuplicateAt(accumulated, window) >= 0;

    /// <summary>Assinatura canônica de uma tool call ("nome(args)") para comparar entre turnos.</summary>
    public static string ToolSignature(string? name, string? arguments)
        => JsonOps.Normalize((name ?? "").Trim().ToLowerInvariant() + "(" + JsonOps.Normalize(arguments ?? "") + ")");

    /// <summary>Assinaturas que já apareceram <paramref name="minCount"/> vezes no histórico.</summary>
    public static HashSet<string> LoopedToolSignatures(System.Text.Json.Nodes.JsonNode? historyMessages, int minCount)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (historyMessages is System.Text.Json.Nodes.JsonArray messages)
        {
            foreach (var message in messages)
            {
                if (message is not System.Text.Json.Nodes.JsonObject obj) continue;
                if (obj["tool_calls"] is not System.Text.Json.Nodes.JsonArray calls) continue;
                foreach (var call in calls)
                {
                    if (call is not System.Text.Json.Nodes.JsonObject callObj) continue;
                    if (callObj["function"] is not System.Text.Json.Nodes.JsonObject function) continue;
                    string signature = ToolSignature(
                        function["name"]?.GetValue<string>(),
                        function["arguments"]?.GetValue<string>());
                    if (signature == "()") continue;
                    counts[signature] = counts.TryGetValue(signature, out int count) ? count + 1 : 1;
                }
            }
        }
        return new HashSet<string>(
            counts.Where(pair => pair.Value >= minCount).Select(pair => pair.Key),
            StringComparer.Ordinal);
    }

    /// <summary>Similaridade 0..1 entre blocos (1 - levenshtein/tamanho).</summary>
    public static double BlockSimilarity(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        int n = a.Length, m = b.Length;
        if (n == 0 || m == 0) return 0;
        if (n > 512 || m > 512) return 0;
        var previous = new int[m + 1];
        var current = new int[m + 1];
        for (int j = 0; j <= m; j++) previous[j] = j;
        for (int i = 1; i <= n; i++)
        {
            current[0] = i;
            for (int j = 1; j <= m; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return 1.0 - (double)previous[m] / Math.Max(n, m);
    }
}
