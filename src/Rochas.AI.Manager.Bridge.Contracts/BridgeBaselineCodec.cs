using System.Text.Json;

namespace Rochas.AI.Manager.Bridge.Contracts;

/// <summary>
/// Leitura/escrita canonica do baseline: parse, hash e validacao em um unico
/// ponto, reusado pelo reader (Infrastructure) e pelo builder (Application).
/// Nao toca em disco nem em banco.
/// </summary>
public static class BridgeBaselineCodec
{
    /// <summary>Opcoes de serializacao (camelCase, null omitido, sem escape).</summary>
    public static readonly JsonSerializerOptions Options = new(ManifestCanonicalizer.SerializerOptions);

    /// <summary>
    /// Le o JSON do baseline, garante o hash do manifesto e valida as regras.
    /// </summary>
    /// <param name="json">Conteudo do seed/manifesto.</param>
    /// <returns>Baseline com <c>experiment.manifestHash</c> preenchido.</returns>
    /// <exception cref="InvalidOperationException">JSON invalido ou manifesto reprovado.</exception>
    public static BridgeBaseline Parse(string json)
    {
        BridgeBaseline? baseline;
        try
        {
            baseline = JsonSerializer.Deserialize<BridgeBaseline>(json, Options);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"baseline: JSON invalido: {exception.Message}", exception);
        }

        if (baseline is null)
            throw new InvalidOperationException("baseline: conteudo vazio");
        if (baseline.Manifest is null)
            throw new InvalidOperationException("baseline: campo 'manifest' obrigatorio");

        var manifest = baseline.Manifest;
        if (string.IsNullOrWhiteSpace(manifest.SchemaVersion))
            manifest.SchemaVersion = BridgeContractVersion.SchemaVersion;

        string computed = ManifestCanonicalizer.ComputeManifestHash(manifest);
        if (!string.IsNullOrWhiteSpace(manifest.Experiment.ManifestHash)
            && !string.Equals(manifest.Experiment.ManifestHash.Trim(), computed, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"baseline: manifestHash nao confere (seed={manifest.Experiment.ManifestHash}, calculado={computed})");
        }
        manifest.Experiment.ManifestHash = computed;

        var validation = BridgeManifestValidator.Validate(manifest);
        if (!validation.IsValid)
        {
            string codes = string.Join("; ", validation.Errors.Select(error => $"{error.Code}@{error.Path}"));
            throw new InvalidOperationException($"baseline: manifesto invalido: {codes}");
        }

        return baseline;
    }

    /// <summary>Serializa o baseline de forma canonica (camelCase).</summary>
    public static string Serialize(BridgeBaseline baseline)
        => JsonSerializer.Serialize(baseline, Options);

    /// <summary>Serializa apenas o manifesto (usado no armazenamento do plano).</summary>
    public static string SerializeManifest(BridgeManifest manifest)
        => JsonSerializer.Serialize(manifest, Options);
}
