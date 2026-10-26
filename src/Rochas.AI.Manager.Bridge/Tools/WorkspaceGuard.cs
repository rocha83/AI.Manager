namespace Rochas.AI.Manager.Bridge.Tools;

/// <summary>
/// Trava de workspace: resolve o caminho pedido e garante que fica dentro da
/// raiz (ou de uma raiz extra explicitamente autorizada pelo host).
/// </summary>
public static class WorkspaceGuard
{
    /// <summary>
    /// Resolve <paramref name="path"/> contra <paramref name="root"/> aplicando o jail.
    /// </summary>
    /// <param name="root">Raiz do workspace.</param>
    /// <param name="path">Caminho relativo ou absoluto pedido pelo modelo.</param>
    /// <param name="full">Caminho absoluto resolvido em caso de sucesso.</param>
    /// <param name="why">Motivo da negacao em caso de falha.</param>
    /// <param name="extraRoots">Raizes adicionais permitidas (politica do host).</param>
    /// <returns><c>true</c> quando o caminho e valido e permitido.</returns>
    public static bool TryResolve(
        string root,
        string path,
        out string full,
        out string why,
        string[]? extraRoots = null)
    {
        full = "";
        why = "";
        try
        {
            // BaseDirectory termina com separador: normaliza para o prefixo nao
            // virar "//" e negar tudo dentro do workspace.
            string baseRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string candidate = Path.GetFullPath(Path.Combine(baseRoot, path ?? ""));
            if (candidate != baseRoot
                && !candidate.StartsWith(baseRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !UnderExtraRoot(candidate, extraRoots ?? ToolContext.DefaultExtraRoots))
            {
                why = $"caminho '{path}' fora do workspace";
                return false;
            }
            full = candidate;
            return true;
        }
        catch (Exception ex)
        {
            why = $"caminho invalido '{path}': {ex.Message}";
            return false;
        }
    }

    private static bool UnderExtraRoot(string candidate, string[] roots)
    {
        foreach (string r in roots)
        {
            if (string.IsNullOrWhiteSpace(r)) continue;
            string root = Path.GetFullPath(r)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (candidate == root
                || candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
