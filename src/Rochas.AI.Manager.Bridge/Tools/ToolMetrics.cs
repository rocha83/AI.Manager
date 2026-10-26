using System.Collections.Concurrent;

namespace Rochas.AI.Manager.Bridge.Tools;

/// <summary>
/// Contadores de tool do host (deny por anel, falhas). Mantido fora do
/// <c>Metrics</c> de transporte para que o pacote de tools nao dependa do
/// estado do proxy; o endpoint <c>/api/metrics</c> expoe os dois.
/// </summary>
public static class ToolMetrics
{
    private static readonly ConcurrentDictionary<string, long> Counters = new(StringComparer.Ordinal);

    /// <summary>Registra uma negacao com anel, tool e motivo.</summary>
    public static void Deny(string ring, string tool, string reason)
        => Counters.AddOrUpdate($"deny:{ring}:{tool}", 1, (_, v) => v + 1);

    /// <summary>Registra uma execucao (sucesso ou falha).</summary>
    public static void Executed(string tool, bool success)
        => Counters.AddOrUpdate($"exec:{(success ? "ok" : "fail")}:{tool}", 1, (_, v) => v + 1);

    /// <summary>Snapshot ordenado dos contadores (nome -> valor).</summary>
    public static IReadOnlyDictionary<string, long> Snapshot()
        => Counters.OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

    /// <summary>Zera os contadores (usado por testes e por reinicio de run).</summary>
    public static void Reset() => Counters.Clear();
}
