namespace Rochas.AI.Manager.Bridge;

/// <summary>
/// Tags de controle que alguns templates deixam vazar no texto (Gemma 4:
/// &lt;|channel&gt;thought ... &lt;channel|&gt;; Qwen: &lt;/thought&gt;). Não são
/// conteúdo: se chegam ao reasoning, a UI mostra a tag crua no bloco de
/// pensamento.
/// </summary>
public static class ControlTags
{
    public static readonly string[] All =
        { "<|channel>", "<channel|>", "<|channel|>", "<thought>", "</thought>", "<thinking>", "</thinking>" };

    /// <summary>Remove as tags completas presentes no texto.</summary>
    public static string Strip(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
        foreach (var tag in All)
            if (text.Contains(tag, StringComparison.OrdinalIgnoreCase))
                text = text.Replace(tag, string.Empty, StringComparison.OrdinalIgnoreCase);
        return text;
    }

    /// <summary>Tamanho do maior sufixo de <paramref name="text"/> que é prefixo de uma tag.</summary>
    /// <remarks>Hold-back do stream: só o prefixo ambíguo fica retido, nunca 12 chars fixos.</remarks>
    public static int PartialLen(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int best = 0;
        foreach (var tag in All)
        {
            int max = Math.Min(tag.Length - 1, text.Length);
            for (int length = max; length > best; length--)
            {
                if (text.EndsWith(tag[..length], StringComparison.OrdinalIgnoreCase))
                {
                    best = length;
                    break;
                }
            }
        }
        return best;
    }

    /// <summary>Tamanho do maior sufixo que é prefixo de uma tag específica (ex.: &lt;think&gt;).</summary>
    public static int PartialPrefixLen(string? text, string tag)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int max = Math.Min(tag.Length - 1, text.Length);
        for (int length = max; length > 0; length--)
            if (text.EndsWith(tag[..length], StringComparison.OrdinalIgnoreCase))
                return length;
        return 0;
    }
}
