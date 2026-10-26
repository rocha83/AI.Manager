using Rochas.AI.Manager.Bridge.Contracts;

namespace Rochas.AI.Manager.Application.TestBench;

/// <summary>
/// Monta o manifesto que o Bridge executa a partir do baseline versionado.
/// O manifesto e imutavel: normalizar recomputa o hash e revalida as regras
/// antes de entregar ao runner. Sem IO e sem banco.
/// </summary>
public static class ManifestBuilder
{
    /// <summary>Le o baseline em JSON e devolve o manifesto normalizado.</summary>
    /// <exception cref="InvalidOperationException">JSON invalido ou manifesto reprovado.</exception>
    public static BridgeManifest FromJson(string json)
        => BridgeBaselineCodec.Parse(json).Manifest;

    /// <summary>
    /// Normaliza um manifesto ja montado: preenche schema/hash e valida.
    /// O hash cobre o manifesto sem o proprio campo.
    /// </summary>
    public static BridgeManifest Normalize(BridgeManifest manifest, bool recomputeHash = false)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (string.IsNullOrWhiteSpace(manifest.SchemaVersion))
            manifest.SchemaVersion = BridgeContractVersion.SchemaVersion;

        string computed = ManifestCanonicalizer.ComputeManifestHash(manifest);
        if (recomputeHash || string.IsNullOrWhiteSpace(manifest.Experiment.ManifestHash))
            manifest.Experiment.ManifestHash = computed;
        else if (!string.Equals(manifest.Experiment.ManifestHash, computed, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"manifestHash nao confere (atual={manifest.Experiment.ManifestHash}, calculado={computed})");

        var validation = BridgeManifestValidator.Validate(manifest);
        if (!validation.IsValid)
        {
            string codes = string.Join("; ", validation.Errors.Select(e => $"{e.Code}@{e.Path}"));
            throw new InvalidOperationException($"manifesto invalido: {codes}");
        }
        return manifest;
    }

    /// <summary>Valida sem lancar: devolve o resultado para a UI mostrar.</summary>
    public static BridgeValidationResult Validate(BridgeManifest manifest)
        => BridgeManifestValidator.Validate(manifest);

    /// <summary>
    /// Constroi o manifesto de execucao para um runner especifico a partir do
    /// baseline: aplica o target do runner e mantem cenarios/stages intactos.
    /// </summary>
    /// <param name="baseline">Baseline do seed.</param>
    /// <param name="runnerId">Runner alvo (<c>q4</c> gate, <c>q3</c> espelho).</param>
    public static BridgeManifest BuildForRunner(BridgeBaseline baseline, string runnerId = "q4")
    {
        ArgumentNullException.ThrowIfNull(baseline);
        var runner = baseline.Runners.FirstOrDefault(r =>
                string.Equals(r.Id, runnerId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"runner '{runnerId}' nao declarado no baseline");

        var manifest = BridgeBaselineCodec.Parse(BridgeBaselineCodec.Serialize(baseline)).Manifest;
        manifest.Target = new BridgeTarget
        {
            BridgeUrl = runner.BridgeUrl,
            UpstreamUrl = runner.UpstreamUrl,
            ModelId = string.IsNullOrWhiteSpace(runner.ModelId)
                ? manifest.Target.ModelId
                : runner.ModelId,
            ContextWindow = runner.ContextWindow > 0 ? runner.ContextWindow : manifest.Target.ContextWindow,
            MaxTokens = runner.MaxTokens > 0 ? runner.MaxTokens : manifest.Target.MaxTokens,
            IsGate = runner.IsGate
        };
        // O target mudou: o hash e recalculado para o manifesto do runner.
        manifest.Experiment.ManifestHash = null;
        return Normalize(manifest, recomputeHash: true);
    }

    /// <summary>
    /// Projeta um plano/cenario persistido de volta ao contrato do Bridge
    /// (usado pela UI e pelo executor quando leem do banco).
    /// </summary>
    public static BridgeScenario ScenarioFrom(
        string key,
        string prompt,
        string? title = null,
        string? systemPrompt = null)
        => new()
        {
            Key = key,
            Title = title ?? key,
            Prompt = prompt,
            SystemPrompt = systemPrompt,
        };

    /// <summary>Resolve o runner de gate declarado no baseline (fallback q4).</summary>
    public static BridgeRunnerProfile GateRunner(BridgeBaseline baseline)
        => baseline.Runners.FirstOrDefault(r => r.IsGate)
           ?? baseline.Runners.FirstOrDefault()
           ?? throw new InvalidOperationException("baseline sem runner declarado");
}
