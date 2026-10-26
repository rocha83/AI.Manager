namespace Rochas.AI.Manager.Bridge;

/// <summary>
/// Sonda de repetição: compara a cauda do texto já emitido com o histórico do
/// próprio stream. O llama não guarda o texto gerado (só n_tokens), então a
/// única prova de loop vem daqui.
/// </summary>
public static class AntiRepeat
{
    public const string DefaultNotice =
        "Aqui e a ponte de contencao: voce repetiu uma chamada/raciocinio identico ja presente no historico e ele foi retido. " +
        "Nao repita: confirme o efeito com read/grep (ou o resultado anterior) e, se faltar correcao, use edit no trecho exato. " +
        "Se nada falta, encerre em silencio.";

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

    /// <summary>
    /// Similaridade minima para repeticao SEMANTICA (mesma ideia/pergunta
    /// reformulada). Abaixo disso o overlap lexical e apenas continuidade normal.
    /// </summary>
    public const double SemanticSimilarity = 0.72;

    /// <summary>Janela minima de tokens de conteudo para a sonda semantica.</summary>
    public const int SemanticWindowTokens = 12;

    /// <summary>
    /// Detecta repeticao semantica: a cauda atual reformula uma janela anterior
    /// (mesmas perguntas/recomendacoes com outras palavras). Diferente da regra
    /// verbatim, ignora ordem, pontuacao e palavras vazias; usa a mesma janela
    /// de caracteres para limitar o lookback.
    /// </summary>
    /// <param name="accumulated">Texto acumulado.</param>
    /// <param name="window">Janela em caracteres (64..2048).</param>
    /// <returns>Posicao (char) do inicio da janela similar, ou -1.</returns>
    public static int SemanticDuplicateAt(string? accumulated, int window)
    {
        if (string.IsNullOrEmpty(accumulated) || window < 32) return -1;

        string norm = JsonOps.Normalize(accumulated);
        if (norm.Length < 2 * window) return -1;

        var tokens = ContentTokens(norm);
        const int size = SemanticWindowTokens;
        if (tokens.Count < size * 2) return -1;

        int queryStart = tokens.Count - size;
        var query = new HashSet<string>(StringComparer.Ordinal);
        for (int i = queryStart; i < tokens.Count; i++) query.Add(tokens[i].Text);
        if (query.Count < size * 3 / 4) return -1;

        int lookback = Math.Min(queryStart, window * 6);
        int minStart = Math.Max(0, queryStart - lookback);
        int maxGap = Math.Max(size, size / 2);

        for (int start = queryStart - maxGap; start >= minStart; start -= Math.Max(4, size / 3))
        {
            int end = start + size;
            if (end > queryStart) continue;

            var candidate = new HashSet<string>(StringComparer.Ordinal);
            for (int i = start; i < end; i++) candidate.Add(tokens[i].Text);
            if (candidate.Count < size * 3 / 4) continue;

            int shared = 0;
            foreach (string token in query)
                if (candidate.Contains(token)) shared++;

            double dice = (2.0 * shared) / (query.Count + candidate.Count);
            if (dice >= SemanticSimilarity && shared >= size * 2 / 3)
                return tokens[start].Start;
        }

        return -1;
    }

    private static List<(string Text, int Start)> ContentTokens(string norm)
    {
        var result = new List<(string Text, int Start)>();
        int i = 0;
        while (i < norm.Length)
        {
            while (i < norm.Length && !char.IsLetterOrDigit(norm[i])) i++;
            int start = i;
            while (i < norm.Length && char.IsLetterOrDigit(norm[i])) i++;
            if (i - start < 3) continue;
            string raw = norm[start..i];
            string token = FoldAccents(raw);
            if (Stopwords.Contains(token)) continue;
            result.Add((token, start));
        }
        return result;
    }

    private static string FoldAccents(string value)
    {
        if (value.IsNormalized(System.Text.NormalizationForm.FormD) is false)
            value = value.Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (char c in value)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }
        return builder.ToString();
    }

    private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "aos", "aquela", "aquele", "aquilo", "aqui", "ate", "com", "como", "da",
        "das", "de", "dela", "dele", "depois", "do", "dos", "e", "ela", "ele",
        "em", "entre", "era", "essa", "esse", "esta", "estao", "este", "faca",
        "foi", "ha", "isso", "isto", "ja", "mais", "mas", "mesmo", "na", "nao",
        "nas", "no", "nos", "nossa", "nosso", "num", "o", "os", "ou", "para",
        "pela", "pelo", "por", "porque", "pois", "que", "se", "sem", "ser",
        "seu", "sua", "tem", "the", "um", "uma", "voce", "and", "are", "for",
        "from", "have", "that", "this", "was", "with", "you", "your"
    };

    /// <summary>Assinatura canônica de uma tool call ("nome(args)") para comparar entre turnos.</summary>
    public static string ToolSignature(string? name, string? arguments)
        => ToolsAntiRepeat.Signature(name, arguments);

    /// <summary>Assinaturas que já apareceram <paramref name="minCount"/> vezes no histórico.</summary>
    public static HashSet<string> LoopedToolSignatures(System.Text.Json.Nodes.JsonNode? historyMessages, int minCount)
        => new RepeatedCall(minCount).Find(historyMessages);

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
