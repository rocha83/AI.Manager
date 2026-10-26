using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Rochas.AI.Manager.Bridge.Contracts;

namespace Rochas.AI.Manager.Bridge.Flow;

/// <summary>Contexto de resolucao do dry-run (placeholders e workspace).</summary>
public sealed class ManifestDryRunOptions
{
    public string? WorkspaceRoot { get; set; }
    public string? RunId { get; set; }
    public string? Model { get; set; }
    public string? Suite { get; set; }
    public long? ExperimentId { get; set; }
    public int? PlanId { get; set; }
}

/// <summary>
/// Resolve o manifesto em comandos CONCRETOS, sem executar nada. E a base do
/// <c>--dry-run</c> e do contrato de paridade com o host legado.
/// </summary>
public static class ManifestCommandResolver
{
    private static readonly Regex PlaceholderRegex = new(@"\{([A-Za-z][A-Za-z0-9_]*)\}", RegexOptions.Compiled);

    public static BridgeManifestDryRun Resolve(BridgeManifest manifest, ManifestDryRunOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        options ??= new ManifestDryRunOptions();

        var steps = new List<BridgeDryRunStep>();
        var issues = new List<BridgeValidationIssue>();

        foreach (var stage in manifest.Stages.OrderBy(stage => stage.Order).ThenBy(stage => stage.Key, StringComparer.Ordinal))
        {
            foreach (var build in stage.Builds)
            {
                var commandLine = BuildCommandLine(build.Command, build.Args, options, issues, $"$.stages[{stage.Key}].builds[{build.Name}]");
                steps.Add(new BridgeDryRunStep(
                    stage.Key,
                    build.Name,
                    "build",
                    "build",
                    commandLine,
                    options.WorkspaceRoot,
                    build.TimeoutSec,
                    Array.Empty<string>()));
            }

            foreach (var check in stage.Preflight)
            {
                var commandLine = ResolvePreflight(check, options, issues, stage.Key);
                steps.Add(new BridgeDryRunStep(
                    stage.Key,
                    $"{stage.Key}:preflight:{steps.Count}",
                    check.Kind,
                    "preflight",
                    commandLine,
                    null,
                    check.TimeoutSec,
                    Array.Empty<string>()));
            }

            foreach (var step in stage.Steps)
            {
                var commandLine = ResolveStep(step, options, issues, stage.Key);
                steps.Add(new BridgeDryRunStep(
                    stage.Key,
                    step.Key,
                    step.Kind,
                    "step",
                    commandLine,
                    IsHttpStep(step) ? null : options.WorkspaceRoot,
                    step.TimeoutSec,
                    step.DependsOn.ToArray()));
            }
        }

        var computed = ManifestCanonicalizer.ComputeManifestHash(manifest);
        var informed = manifest.Experiment.ManifestHash;
        var matches = string.IsNullOrWhiteSpace(informed)
            || string.Equals(informed.Trim(), computed, StringComparison.OrdinalIgnoreCase);

        if (!matches)
        {
            issues.Add(new BridgeValidationIssue(
                "manifest.hash.mismatch",
                "$.experiment.manifestHash",
                $"informado {informed} difere do calculado {computed}"));
        }

        return new BridgeManifestDryRun(
            manifest.SchemaVersion,
            informed,
            computed,
            matches,
            steps,
            issues);
    }

    private static string ResolveStep(
        BridgeStep step,
        ManifestDryRunOptions options,
        List<BridgeValidationIssue> issues,
        string stageKey)
    {
        var path = $"$.stages[{stageKey}].steps[{step.Key}]";
        return step.Kind.ToLowerInvariant() switch
        {
            "http" => step.Args.Count > 0
                ? $"GET {ResolvePlaceholders(step.Args[0], options, issues, path)}"
                : $"HTTP {step.Key}",
            "native" => $"native:{step.Key}",
            "deploy" => BuildCommandLine(
                string.IsNullOrWhiteSpace(step.Command) ? "dotnet" : step.Command,
                step.Args.Count > 0 ? step.Args : new List<string> { "publish", "-c", "Release" },
                options,
                issues,
                path),
            _ => BuildCommandLine(step.Command, step.Args, options, issues, path)
        };
    }

    private static string ResolvePreflight(
        BridgePreflightCheck check,
        ManifestDryRunOptions options,
        List<BridgeValidationIssue> issues,
        string stageKey)
    {
        var path = $"$.stages[{stageKey}].preflight";

        return check.Kind.ToLowerInvariant() switch
        {
            "http-json" => $"GET {ResolvePlaceholders(check.Url, options, issues, path)} (json)",
            "bridge-status" => $"GET {ResolvePlaceholders(check.Url, options, issues, path)} (bridge-status)",
            "command" => BuildCommandLine(check.Command, check.Args, options, issues, path),
            "file-exists" => $"test -e {ResolvePlaceholders(check.Path, options, issues, path)}",
            "port-open" => $"port-open {ResolvePlaceholders(check.Url, options, issues, path)}",
            _ => $"{check.Kind} (desconhecido)"
        };
    }

    private static string BuildCommandLine(
        string? command,
        IReadOnlyList<string> args,
        ManifestDryRunOptions options,
        List<BridgeValidationIssue> issues,
        string path)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            issues.Add(new BridgeValidationIssue("dryRun.command.empty", path, "comando ausente no manifesto"));
            return "(sem comando)";
        }

        var builder = new StringBuilder();
        builder.Append(Quote(ResolvePlaceholders(command, options, issues, path)));
        foreach (var arg in args)
        {
            builder.Append(' ');
            builder.Append(Quote(ResolvePlaceholders(arg, options, issues, path)));
        }
        return builder.ToString();
    }

    private static string ResolvePlaceholders(
        string? text,
        ManifestDryRunOptions options,
        List<BridgeValidationIssue> issues,
        string path)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        return PlaceholderRegex.Replace(text, match =>
        {
            var token = match.Groups[1].Value;
            var resolved = token switch
            {
                "ws" => options.WorkspaceRoot,
                "runId" => options.RunId,
                "model" => options.Model,
                "suite" => options.Suite,
                "experimentId" => options.ExperimentId?.ToString(CultureInfo.InvariantCulture),
                "planId" => options.PlanId?.ToString(CultureInfo.InvariantCulture),
                "case" => null,
                _ => null
            };

            if (!BridgeManifestRules.Placeholders.Contains(token))
            {
                issues.Add(new BridgeValidationIssue(
                    "dryRun.placeholder.unknown",
                    path,
                    $"placeholder '{{{token}}}' desconhecido; mantido literal"));
                return match.Value;
            }

            if (resolved is null)
            {
                issues.Add(new BridgeValidationIssue(
                    "dryRun.placeholder.unresolved",
                    path,
                    $"placeholder '{{{token}}}' sem valor no contexto do dry-run; mantido literal"));
                return match.Value;
            }

            return resolved;
        });
    }

    private static bool IsHttpStep(BridgeStep step) =>
        step.Kind.Equals("http", StringComparison.OrdinalIgnoreCase);

    private static string Quote(string value)
    {
        if (value.Length == 0) return "\"\"";
        if (value.All(c => !char.IsWhiteSpace(c) && c != '"' && c != '\'' && c != '\\'))
            return value;
        return "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }
}
