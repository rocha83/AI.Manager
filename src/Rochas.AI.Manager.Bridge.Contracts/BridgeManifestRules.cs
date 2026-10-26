namespace Rochas.AI.Manager.Bridge.Contracts;

/// <summary>
/// Regras declarativas do manifesto. A UI so configura parametros; kind de step e
/// tipo de assertion novos exigem release do Bridge (allowlist em codigo).
/// </summary>
public static class BridgeManifestRules
{
    public static readonly IReadOnlySet<string> StepKinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "dotnet-test", "web-test", "build", "deploy", "http", "shell", "native"
    };

    public static readonly IReadOnlySet<string> PreflightKinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "http-json", "bridge-status", "command", "file-exists", "port-open"
    };

    public static readonly IReadOnlySet<string> AssertionTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "fileCountAtLeast",
        "totalCharsAtLeast",
        "fileExtensionCountAtLeast",
        "fileExists",
        "fileAbsent",
        "commandExitZero",
        "buildSucceeds",
        "noProseBeforeTool"
    };

    public static readonly IReadOnlySet<string> ActionModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "required", "auto", "none"
    };

    public static readonly IReadOnlySet<string> OnFailureModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "continue", "stop", "retry"
    };

    public static readonly IReadOnlySet<string> Placeholders = new HashSet<string>(StringComparer.Ordinal)
    {
        "ws", "case", "runId", "model", "suite", "planId", "experimentId"
    };
}
