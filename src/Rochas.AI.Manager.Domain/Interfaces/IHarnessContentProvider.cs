namespace Rochas.AI.Manager.Domain.Interfaces;

using Rochas.AI.Manager.Domain.Harness;

/// <summary>
/// Fonte dos enunciados de harness (caudas). A implementação lê os
/// <see cref="HarnessAttachment"/> da versão vigente; o motor só usa defaults
/// quando não há dado.
/// </summary>
public interface IHarnessContentProvider
{
    Task<HarnessContent> GetAsync(CancellationToken ct = default);
}
