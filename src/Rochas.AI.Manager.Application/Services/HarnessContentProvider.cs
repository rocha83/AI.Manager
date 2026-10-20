using Rochas.AI.Manager.Domain.Harness;
using Rochas.AI.Manager.Domain.Interfaces;
using Rochas.Data.Specification.Interfaces;

namespace Rochas.AI.Manager.Application.Services;

/// <summary>
/// Lê os enunciados de harness da versão mais recente do perfil. Cada
/// <see cref="HarnessAttachment.Kind"/> é uma peça independente:
/// Core (regras gerais), Nucleo (disciplina por tool), Tools (catálogo),
/// Behavior (workspace/formato) e ToolsUsage (como chamar tool). O anti-repeat
/// NÃO é dado: é lógica do motor (janela, retry, detecção).
/// </summary>
public sealed class HarnessContentProvider : IHarnessContentProvider
{
    private readonly IGenericRepository<HarnessAttachment> attachmentRepository;

    public HarnessContentProvider(IGenericRepository<HarnessAttachment> attachmentRepository)
    {
        this.attachmentRepository = attachmentRepository;
    }

    public async Task<HarnessContent> GetAsync(CancellationToken ct = default)
    {
        var attachments = await attachmentRepository.Query(new HarnessAttachment());
        if (attachments.Count == 0) return HarnessContent.Empty;

        int versionId = attachments.Max(attachment => attachment.VersionId);
        var current = attachments
            .Where(attachment => attachment.VersionId == versionId && attachment.Role == "general")
            .ToList();

        string? Find(string kind) => current
            .FirstOrDefault(attachment => attachment.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase))
            ?.Content;

        return new HarnessContent(
            Core: Find("Core"),
            ToolCatalog: Find("Tools"),
            Nucleo: Find("Nucleo"),
            Behavior: Find("Behavior"),
            ToolsUsage: Find("ToolsUsage"));
    }
}
