using Rochas.AI.Manager.Bridge.Contracts;

namespace Rochas.AI.Manager.Infrastructure.TestBench;

/// <summary>
/// Le um baseline de testes versionado (<c>war-baseline.json</c>) e devolve o
/// envelope com manifesto validado e hash conferido. Nao toca no banco: a
/// persistencia e responsabilidade do <see cref="TestBenchSeeder"/>.
/// </summary>
public static class WarBaselineReader
{
    /// <summary>Le o arquivo do disco e valida o manifesto.</summary>
    /// <param name="path">Caminho do seed (<c>war-baseline.json</c>).</param>
    /// <exception cref="FileNotFoundException">Arquivo inexistente.</exception>
    /// <exception cref="InvalidOperationException">Manifesto invalido ou hash divergente.</exception>
    public static BridgeBaseline Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("path do baseline e obrigatorio", nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException($"baseline nao encontrado: '{path}'", path);
        return LoadFromJson(File.ReadAllText(path));
    }

    /// <summary>Le o JSON em memoria e valida o manifesto.</summary>
    public static BridgeBaseline LoadFromJson(string json)
        => BridgeBaselineCodec.Parse(json ?? string.Empty);

    /// <summary>
    /// Resolve o caminho do seed dentro do deployment, com override por ambiente
    /// (<c>AI_MANAGER_WAR_BASELINE</c>) para o runner de teste.
    /// </summary>
    public static string ResolveSeedPath(string? explicitPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath)) return explicitPath!;
        string? env = Environment.GetEnvironmentVariable("AI_MANAGER_WAR_BASELINE");
        if (!string.IsNullOrWhiteSpace(env)) return env!;

        string local = Path.Combine(AppContext.BaseDirectory, "Seeds", "war-baseline.json");
        if (File.Exists(local)) return local;

        // Repo layout: src/<projeto>/bin/... -> ../../../../Data/Seeds.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "Data", "Seeds", "war-baseline.json");
            if (File.Exists(candidate)) return candidate;
        }
        return local;
    }
}
