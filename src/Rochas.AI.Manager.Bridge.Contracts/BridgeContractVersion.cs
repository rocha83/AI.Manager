namespace Rochas.AI.Manager.Bridge.Contracts;

/// <summary>
/// Versao unica do contrato publico do componente. O schema do manifesto e o
/// contrato REST caminham juntos: minor aditivo, major para quebra.
/// </summary>
public static class BridgeContractVersion
{
    /// <summary>Versao do schema do manifesto (campo <c>schemaVersion</c>).</summary>
    public const string SchemaVersion = "1.0";

    /// <summary>Versao SemVer do contrato REST/DTOs exposta em <c>/api/contract</c>.</summary>
    public const string ContractVersion = "1.0.0";

    /// <summary>Nome canonico do componente.</summary>
    public const string ComponentName = "Rochas.AI.Manager.Bridge";

    /// <summary>Schemas de manifesto aceitos por esta versao do componente.</summary>
    public static readonly IReadOnlyList<string> SupportedSchemaVersions = new[] { SchemaVersion };

    /// <summary>Schemas de manifesto ja publicados e ainda legiveis.</summary>
    public static readonly IReadOnlyList<string> KnownSchemaVersions = new[] { SchemaVersion };

    /// <summary>O schema informado e aceito pelo componente?</summary>
    public static bool IsSchemaSupported(string? schemaVersion) =>
        !string.IsNullOrWhiteSpace(schemaVersion)
        && SupportedSchemaVersions.Contains(schemaVersion.Trim(), StringComparer.Ordinal);

    /// <summary>
    /// Compatibilidade de contrato: mesmo major e minor remoto menor ou igual ao
    /// local. Major diferente e quebra; minor remoto maior exige componente novo.
    /// </summary>
    public static bool IsContractCompatible(string? remoteVersion)
    {
        if (!TryParse(remoteVersion, out var remote)) return false;
        if (!TryParse(ContractVersion, out var local)) return false;
        return remote.Major == local.Major && remote.Minor <= local.Minor;
    }

    private static bool TryParse(string? text, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(text)) return false;

        // SemVer pode vir com pre-release/build: "1.0.0-rc.1" -> "1.0.0".
        var core = text.Trim();
        var cut = core.IndexOfAny(new[] { '-', '+' });
        if (cut >= 0) core = core[..cut];

        // "1.0" tambem e aceito.
        if (core.Count(c => c == '.') == 1) core += ".0";
        return Version.TryParse(core, out version!);
    }
}
