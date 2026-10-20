namespace Rochas.AI.Manager.Domain.Harness;

/// <summary>
/// Enunciados de harness carregados do banco. Textos são DADOS da plataforma
/// (seed/edição na UI); o código do motor só tem defaults de emergência.
/// </summary>
public sealed record HarnessContent(
    string? Core,
    string? ToolCatalog,
    string? Nucleo,
    string? Behavior,
    string? ToolsUsage)
{
    public static HarnessContent Empty { get; } = new(null, null, null, null, null);

    public bool IsEmpty =>
        Core is null && ToolCatalog is null && Nucleo is null
        && Behavior is null && ToolsUsage is null;
}
