using System.Collections.Concurrent;

namespace Rochas.AI.Manager.Bridge.Transport;

/// <summary>
/// Contadores de transporte do host do Bridge (requisicoes, streams, erros e
/// contingencias de anti-repeat). O endpoint <c>/api/metrics</c> expoe o
/// snapshot; nenhum contador altera o comportamento do protocolo.
/// </summary>
public static class BridgeMetrics
{
    private static readonly ConcurrentDictionary<string, long> Counters = new(StringComparer.Ordinal);

    /// <summary>Incrementa um contador nomeado.</summary>
    public static void Inc(string name, long delta = 1)
        => Counters.AddOrUpdate(name, delta, (_, v) => v + delta);

    /// <summary>Snapshot ordenado dos contadores.</summary>
    public static IReadOnlyDictionary<string, long> Snapshot()
        => Counters.OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

    /// <summary>Zera os contadores (testes e reinicio de run).</summary>
    public static void Reset() => Counters.Clear();
}
