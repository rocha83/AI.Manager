using System.Reflection;
using Rochas.AI.Manager.Bridge.Contracts;

namespace Rochas.AI.Manager.Bridge.Hosting;

/// <summary>Identidade e status do componente expostos no handshake do host.</summary>
public static class BridgeComponent
{
    public static string Version { get; } = ResolveVersion();

    public static IReadOnlyList<string> Features { get; } = new[]
    {
        "responses-to-chat",
        "chat-to-responses",
        "responses-stream",
        "anti-repeat",
        "tool-args-anti-repeat",
        "breaker",
        "context-guard",
        "main-harness",
        "harness-tails",
        "tail-scope",
        "client-discriminator",
        "legacy-harness",
        "qwen-standard",
        "tool-strategy",
        "tool-coercion",
        "text-grammar",
        "manifest-contract",
        "manifest-validate",
        "manifest-dry-run",
        "tool-shim"
    };

    public static BridgeComponentInfo Info() => new(
        BridgeContractVersion.ComponentName,
        Version,
        BridgeContractVersion.ContractVersion,
        BridgeContractVersion.SchemaVersion,
        BridgeContractVersion.SupportedSchemaVersions,
        Features);

    public static BridgeStatusResponse Status(BridgeHostOptions options, DateTimeOffset startedAt)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new BridgeStatusResponse(
            BridgeContractVersion.ComponentName,
            Version,
            BridgeContractVersion.ContractVersion,
            BridgeContractVersion.SchemaVersion,
            startedAt,
            (long)Math.Max(0, (DateTimeOffset.UtcNow - startedAt).TotalSeconds),
            options.UpstreamUrl,
            options.ModelId,
            options.IsGate,
            options.Port,
            options.Listen,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["role"] = options.IsGate ? "gate" : "mirror",
                ["artifactRoot"] = options.ArtifactRoot ?? string.Empty
            });
    }

    private static string ResolveVersion()
    {
        var assembly = typeof(BridgeComponent).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var cut = informational.IndexOf('+');
            return cut >= 0 ? informational[..cut] : informational;
        }
        return assembly.GetName().Version?.ToString(3) ?? BridgeContractVersion.ContractVersion;
    }
}
