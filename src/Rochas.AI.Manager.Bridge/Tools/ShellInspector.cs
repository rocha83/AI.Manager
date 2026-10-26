using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Rochas.AI.Manager.Bridge.Tools;

/// <summary>
/// Inspetor pre-execucao do shell: negacao uniforme (comando direto, cadeia
/// <c>&amp;&amp;</c>/<c>;</c>, payload de <c>bash -c</c>) e classificacao para os
/// gates de permissao. Fail-closed: duvida nega com motivo e o modelo reformula.
/// </summary>
public static class ShellInspector
{
    /// <summary>Classe do comando para a politica de permissao do host.</summary>
    public enum ShellClass
    {
        /// <summary>Comando generico de leitura/consulta.</summary>
        Other,
        /// <summary>Validacao de sintaxe (<c>bash -n</c>).</summary>
        Validation,
        /// <summary>Execucao de script pelo interpretador.</summary>
        Execution,
        /// <summary>Inspecao de processos (<c>ps</c>, <c>top -b</c>).</summary>
        Proc,
        /// <summary>Instalacao de pacotes (<c>pip</c>, <c>apt</c>).</summary>
        Install
    }

    private static readonly string[] DeniedTokens =
    {
        "rm", "kill", "pkill", "killall", "reboot", "shutdown", "halt",
        "mkfs", "dd", "fdisk", "mount", "systemctl", "service", "crontab",
        "ssh", "scp", "chmod", "chown", "nohup", "setsid", "disown",
        "exec", "eval", "su", "mkfifo", "mv", "rmdir", "truncate", "shred",
    };

    private static readonly string[] DeniedPatterns =
    {
        "/dev/sd", "/dev/nvme", "/dev/hd", "/dev/vd", "/dev/mmcblk", "of=/dev/", ":(){",
        "reset --hard",
        "os.remove", "os.unlink", "unlinkSync", "rmSync", "rmtree", "shutil.rmtree", "fs.unlink",
    };

    private static readonly string[] PayloadBins =
    {
        "bash", "sh", "shell", "zsh", "dash", "python", "python3", "node", "perl", "ruby",
    };

    private static readonly string[] PayloadFlags = { "-c", "-e", "-p", "--eval", "--command" };

    private static readonly Regex PipeToShell = new(
        @"\|\s*(?:sudo\s+)?(?:bash|sh|zsh|dash|python3?|node|perl|ruby)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Extrai o campo <c>command</c> do JSON (ou devolve o texto cru).</summary>
    public static string ExtractCommand(string argumentsJson)
    {
        try
        {
            var obj = JsonNode.Parse(argumentsJson)?.AsObject();
            if (obj?["command"]?.GetValue<string>() is string cmd && !string.IsNullOrWhiteSpace(cmd))
                return cmd.Trim();
        }
        catch { /* nao e JSON: usa cru */ }
        return (argumentsJson ?? "").Trim();
    }

    /// <summary>Primeiro token do comando (binario), sem consumir argumentos.</summary>
    public static string FirstToken(string command)
    {
        string t = (command ?? "").TrimStart();
        int i = t.IndexOfAny(new[] { ' ', '\t' });
        return i < 0 ? t : t[..i];
    }

    /// <summary>Valida o comando; <c>null</c> libera, texto devolve o motivo da negacao.</summary>
    public static string? Validate(string command) => Validate(command, 0);

    private static string? Validate(string command, int depth)
    {
        string cmd = command ?? "";
        if (string.IsNullOrWhiteSpace(cmd)) return "shell comando vazio";
        if (depth > 3) return "payload de interpretador aninhado demais: use a tool direta";

        string padded = " " + cmd + " ";
        foreach (string bad in DeniedTokens)
        {
            string pattern = $@"[\s\""'(/|;&=]{Regex.Escape(bad)}[\s\""' )/|;&=]";
            if (Regex.IsMatch(padded, pattern, RegexOptions.IgnoreCase))
                return $"token '{bad}' negado pelo inspetor";
        }

        foreach (string pat in DeniedPatterns)
            if (cmd.Contains(pat, StringComparison.Ordinal))
                return $"padrao '{pat}' negado pelo inspetor";

        if (Regex.IsMatch(cmd, @"\breset\s+--hard\b", RegexOptions.IgnoreCase))
            return "git reset --hard negado: apaga trabalho sem volta";

        if (SedInPlace(cmd))
            return "sed -i (in-place) negado: use a tool edit no trecho exato";

        if (PipeToShell.IsMatch(cmd))
            return "pipe para interpretador negado: leia com fetch/read e rode o comando direto";

        if (PayloadOf(cmd, out string? payload))
        {
            if (string.IsNullOrWhiteSpace(payload))
                return "interpretador sem payload: informe o comando ou use a tool direta";
            if (Validate(payload, depth + 1) is string inner)
                return $"dentro de {FirstToken(cmd)}: {inner}";
        }

        if (string.Equals(FirstToken(cmd), "top", StringComparison.OrdinalIgnoreCase)
            && !Regex.IsMatch(cmd, @"(?:^|\s)-[a-zA-Z]*[bn]"))
            return "top interativo negado: use 'top -b -n 1'";

        return null;
    }

    private static bool SedInPlace(string cmd)
    {
        var m = Regex.Match(cmd, @"\bsed\b([^|;&]*)", RegexOptions.IgnoreCase);
        if (!m.Success) return false;
        foreach (string tok in m.Groups[1].Value.Split(
                     new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (tok.StartsWith("--"))
                return tok.Equals("--in-place", StringComparison.OrdinalIgnoreCase)
                    || tok.StartsWith("--in-place=", StringComparison.OrdinalIgnoreCase);
            if (!tok.StartsWith("-")) return false;   // primeiro operando: acabaram as flags
            if (tok[1..].Contains('i')) return true;  // -i, -Ei, -ni, -i.bak
        }
        return false;
    }

    private static bool PayloadOf(string cmd, out string? payload)
    {
        payload = null;
        string t = (cmd ?? "").TrimStart();
        int sp = t.IndexOfAny(new[] { ' ', '\t' });
        if (sp <= 0) return false;
        string bin = t[..sp];
        if (!PayloadBins.Any(b => string.Equals(b, bin, StringComparison.OrdinalIgnoreCase))) return false;

        var toks = Tokenize(t[(sp + 1)..]);
        for (int i = 0; i < toks.Count; i++)
        {
            string tok = toks[i];
            if (!tok.StartsWith("-")) return false;
            bool inline = PayloadFlags.Any(f => string.Equals(f, tok, StringComparison.OrdinalIgnoreCase))
                || (tok.Length > 1 && !tok.StartsWith("--")
                    && (tok[1..].Contains('c') || tok[1..].Contains('e')));
            if (!inline) continue;
            payload = i + 1 < toks.Count ? toks[i + 1] : "";
            return true;
        }
        return false;
    }

    private static List<string> Tokenize(string s)
    {
        var list = new List<string>();
        var sb = new StringBuilder();
        char quote = '\0';
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (quote != '\0')
            {
                if (c == quote) { quote = '\0'; continue; }
                if (c == '\\' && i + 1 < s.Length && quote == '"') { sb.Append(s[++i]); continue; }
                sb.Append(c);
            }
            else if (c is '"' or '\'') quote = c;
            else if (char.IsWhiteSpace(c))
            {
                if (sb.Length > 0) { list.Add(sb.ToString()); sb.Clear(); }
            }
            else sb.Append(c);
        }
        if (sb.Length > 0) list.Add(sb.ToString());
        return list;
    }

    /// <summary>Classifica o comando para os gates de permissao.</summary>
    public static ShellClass Classify(string command)
    {
        string first = FirstToken(command);
        if (first is "ps" or "top" or "pgrep") return ShellClass.Proc;
        if (first is "pip" or "pip3" or "apt" or "apt-get") return ShellClass.Install;
        if (first is "bash" or "sh" or "shell")
            return Regex.IsMatch(command ?? "", @"(?:^|\s)-n(?:\s|$)")
                ? ShellClass.Validation
                : ShellClass.Execution;
        return ShellClass.Other;
    }
}
