using System.Text.RegularExpressions;

namespace Rochas.AI.Manager.Bridge.Contracts;

/// <summary>
/// Valida o manifesto ANTES de executar: schema, identidade, alvo, capabilities,
/// kinds, dependencias, seletores e assertions. Erro aqui = Blocked, sem rodar.
/// </summary>
public static class BridgeManifestValidator
{
    private static readonly Regex PlaceholderRegex = new(@"\{([A-Za-z][A-Za-z0-9_]*)\}", RegexOptions.Compiled);

    public static BridgeValidationResult Validate(BridgeManifest? manifest)
    {
        var errors = new List<BridgeValidationIssue>();
        var warnings = new List<BridgeValidationIssue>();

        if (manifest is null)
            return BridgeValidationResult.Invalid(new BridgeValidationIssue(
                "manifest.null", "$", "manifesto ausente"));

        if (!BridgeContractVersion.IsSchemaSupported(manifest.SchemaVersion))
        {
            errors.Add(new BridgeValidationIssue(
                "schema.unsupported",
                "$.schemaVersion",
                $"schemaVersion '{manifest.SchemaVersion}' nao suportado; aceitos: {string.Join(", ", BridgeContractVersion.SupportedSchemaVersions)}"));
        }

        ValidateExperiment(manifest.Experiment, errors);
        ValidateRunner(manifest.Runner, errors);
        ValidateTarget(manifest.Target, errors, warnings);
        ValidateHarness(manifest.Harness, warnings);

        if (manifest.Stages.Count == 0)
            errors.Add(new BridgeValidationIssue("stages.empty", "$.stages", "manifesto sem stage"));

        var stageKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var stage in manifest.Stages)
        {
            ValidateStage(stage, stageKeys, errors, warnings);
        }

        var scenarioKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var scenario in manifest.Scenarios)
        {
            ValidateScenario(scenario, scenarioKeys, errors, warnings);
        }

        // Steps que apontam para cenario precisam de cenario existente.
        foreach (var step in manifest.Stages.SelectMany(stage => stage.Steps))
        {
            var selected = step.ScenarioSelector?.Cases ?? new List<string>();
            foreach (var scenarioKey in selected)
            {
                if (!scenarioKeys.Contains(scenarioKey))
                {
                    errors.Add(new BridgeValidationIssue(
                        "step.scenario.unknown",
                        $"$.stages[*].steps[{step.Key}].scenarioSelector.cases",
                        $"cenario '{scenarioKey}' nao existe em $.scenarios"));
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(manifest.Experiment.ManifestHash)
            && !ManifestCanonicalizer.VerifyManifestHash(manifest))
        {
            errors.Add(new BridgeValidationIssue(
                "manifest.hash.mismatch",
                "$.experiment.manifestHash",
                "manifestHash informado nao confere com o conteudo canonico"));
        }

        return new BridgeValidationResult(errors.Count == 0, errors, warnings);
    }

    /// <summary>Valida e devolve o hash calculado (usado pelo builder/dry-run).</summary>
    public static string ComputeHash(BridgeManifest manifest) =>
        ManifestCanonicalizer.ComputeManifestHash(manifest);

    private static void ValidateExperiment(BridgeExperimentRef experiment, List<BridgeValidationIssue> errors)
    {
        if (string.IsNullOrWhiteSpace(experiment.Name))
            errors.Add(new BridgeValidationIssue("experiment.name.empty", "$.experiment.name", "nome do experimento e obrigatorio"));
        if (experiment.Version < 0)
            errors.Add(new BridgeValidationIssue("experiment.version.invalid", "$.experiment.version", "versao nao pode ser negativa"));
    }

    private static void ValidateRunner(BridgeRunnerRef runner, List<BridgeValidationIssue> errors)
    {
        if (string.IsNullOrWhiteSpace(runner.Id))
            errors.Add(new BridgeValidationIssue("runner.id.empty", "$.runner.id", "runner.id obrigatorio"));
        if (runner.Capabilities.Count == 0)
            errors.Add(new BridgeValidationIssue("runner.capabilities.empty", "$.runner.capabilities", "runner sem capabilities declaradas"));
    }

    private static void ValidateTarget(BridgeTarget target, List<BridgeValidationIssue> errors, List<BridgeValidationIssue> warnings)
    {
        if (!IsHttpUrl(target.BridgeUrl, out var bridgeError))
            errors.Add(new BridgeValidationIssue("target.bridgeUrl.invalid", "$.target.bridgeUrl", bridgeError!));
        if (!IsHttpUrl(target.UpstreamUrl, out var upstreamError))
            errors.Add(new BridgeValidationIssue("target.upstreamUrl.invalid", "$.target.upstreamUrl", upstreamError!));
        if (string.IsNullOrWhiteSpace(target.ModelId))
            warnings.Add(new BridgeValidationIssue("target.modelId.empty", "$.target.modelId", "modelId vazio; o upstream decide o modelo"));
        if (target.ContextWindow < 0)
            errors.Add(new BridgeValidationIssue("target.contextWindow.invalid", "$.target.contextWindow", "contextWindow nao pode ser negativo"));
        if (target.MaxTokens < 0)
            errors.Add(new BridgeValidationIssue("target.maxTokens.invalid", "$.target.maxTokens", "maxTokens nao pode ser negativo"));
    }

    private static void ValidateHarness(BridgeHarnessRef? harness, List<BridgeValidationIssue> warnings)
    {
        if (harness is null)
        {
            warnings.Add(new BridgeValidationIssue("harness.absent", "$.harness", "manifesto sem snapshot de harness"));
            return;
        }
        if (string.IsNullOrWhiteSpace(harness.Mode))
            warnings.Add(new BridgeValidationIssue("harness.mode.empty", "$.harness.mode", "mode do harness vazio"));
        if (harness.ProfileVersionId <= 0)
            warnings.Add(new BridgeValidationIssue("harness.profileVersionId.invalid", "$.harness.profileVersionId", "profileVersionId nao informado"));
    }

    private static void ValidateStage(
        BridgeStage stage,
        HashSet<string> stageKeys,
        List<BridgeValidationIssue> errors,
        List<BridgeValidationIssue> warnings)
    {
        if (string.IsNullOrWhiteSpace(stage.Key))
        {
            errors.Add(new BridgeValidationIssue("stage.key.empty", "$.stages[*].key", "stage.key obrigatorio"));
        }
        else if (!stageKeys.Add(stage.Key))
        {
            errors.Add(new BridgeValidationIssue("stage.key.duplicate", "$.stages[*].key", $"stage '{stage.Key}' duplicado"));
        }

        if (stage.Order < 0)
            errors.Add(new BridgeValidationIssue("stage.order.invalid", $"$.stages[{stage.Key}].order", "order nao pode ser negativo"));
        if (stage.MaxParallelSteps < 1)
            errors.Add(new BridgeValidationIssue("stage.maxParallelSteps.invalid", $"$.stages[{stage.Key}].maxParallelSteps", "maxParallelSteps deve ser >= 1"));
        if (!BridgeManifestRules.OnFailureModes.Contains(stage.OnFailure))
            errors.Add(new BridgeValidationIssue("stage.onFailure.invalid", $"$.stages[{stage.Key}].onFailure", $"onFailure '{stage.OnFailure}' invalido"));

        foreach (var build in stage.Builds)
        {
            if (string.IsNullOrWhiteSpace(build.Command))
                errors.Add(new BridgeValidationIssue("build.command.empty", $"$.stages[{stage.Key}].builds[{build.Name}]", "build.command obrigatorio"));
            if (build.TimeoutSec < 0)
                errors.Add(new BridgeValidationIssue("build.timeout.invalid", $"$.stages[{stage.Key}].builds[{build.Name}]", "timeoutSec nao pode ser negativo"));
        }

        foreach (var check in stage.Preflight)
        {
            if (!BridgeManifestRules.PreflightKinds.Contains(check.Kind))
                errors.Add(new BridgeValidationIssue("preflight.kind.unknown", $"$.stages[{stage.Key}].preflight", $"preflight kind '{check.Kind}' desconhecido"));
            if (check.Kind.Equals("http-json", StringComparison.OrdinalIgnoreCase) && !IsHttpUrl(check.Url, out var urlError))
                errors.Add(new BridgeValidationIssue("preflight.url.invalid", $"$.stages[{stage.Key}].preflight", urlError!));
            if (check.Kind.Equals("command", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(check.Command))
                errors.Add(new BridgeValidationIssue("preflight.command.empty", $"$.stages[{stage.Key}].preflight", "preflight command exige command"));
            if (check.Kind.Equals("file-exists", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(check.Path))
                errors.Add(new BridgeValidationIssue("preflight.path.empty", $"$.stages[{stage.Key}].preflight", "preflight file-exists exige path"));
        }

        var stepKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in stage.Steps)
        {
            ValidateStep(stage.Key, step, stepKeys, errors, warnings);
        }

        // Dependencias e ramificacoes so podem apontar para steps do mesmo stage.
        foreach (var step in stage.Steps)
        {
            foreach (var dependency in step.DependsOn)
            {
                if (!stepKeys.Contains(dependency))
                    errors.Add(new BridgeValidationIssue("step.dependsOn.unknown", $"$.stages[{stage.Key}].steps[{step.Key}].dependsOn", $"step '{dependency}' nao existe no stage"));
            }
            foreach (var next in step.NextSteps)
            {
                if (!stepKeys.Contains(next))
                    errors.Add(new BridgeValidationIssue("step.nextSteps.unknown", $"$.stages[{stage.Key}].steps[{step.Key}].nextSteps", $"step '{next}' nao existe no stage"));
            }
            if (step.DependsOn.Contains(step.Key, StringComparer.OrdinalIgnoreCase))
                errors.Add(new BridgeValidationIssue("step.dependsOn.self", $"$.stages[{stage.Key}].steps[{step.Key}].dependsOn", "step nao pode depender de si mesmo"));
        }
    }

    private static void ValidateStep(
        string stageKey,
        BridgeStep step,
        HashSet<string> stepKeys,
        List<BridgeValidationIssue> errors,
        List<BridgeValidationIssue> warnings)
    {
        if (string.IsNullOrWhiteSpace(step.Key))
        {
            errors.Add(new BridgeValidationIssue("step.key.empty", $"$.stages[{stageKey}].steps[*].key", "step.key obrigatorio"));
            return;
        }
        if (!stepKeys.Add(step.Key))
            errors.Add(new BridgeValidationIssue("step.key.duplicate", $"$.stages[{stageKey}].steps[{step.Key}]", $"step '{step.Key}' duplicado no stage"));

        if (!BridgeManifestRules.StepKinds.Contains(step.Kind))
            errors.Add(new BridgeValidationIssue("step.kind.unknown", $"$.stages[{stageKey}].steps[{step.Key}].kind", $"step kind '{step.Kind}' desconhecido"));

        if (step.TimeoutSec < 0)
            errors.Add(new BridgeValidationIssue("step.timeout.invalid", $"$.stages[{stageKey}].steps[{step.Key}].timeoutSec", "timeoutSec nao pode ser negativo"));

        if (step.Retry is { MaxAttempts: < 1 })
            errors.Add(new BridgeValidationIssue("step.retry.invalid", $"$.stages[{stageKey}].steps[{step.Key}].retry", "maxAttempts deve ser >= 1"));

        foreach (var token in step.Args.SelectMany(FindPlaceholders))
        {
            if (!BridgeManifestRules.Placeholders.Contains(token))
                warnings.Add(new BridgeValidationIssue("step.placeholder.unknown", $"$.stages[{stageKey}].steps[{step.Key}].args", $"placeholder '{{{token}}}' desconhecido"));
        }
    }

    private static void ValidateScenario(
        BridgeScenario scenario,
        HashSet<string> scenarioKeys,
        List<BridgeValidationIssue> errors,
        List<BridgeValidationIssue> warnings)
    {
        if (string.IsNullOrWhiteSpace(scenario.Key))
        {
            errors.Add(new BridgeValidationIssue("scenario.key.empty", "$.scenarios[*].key", "scenario.key obrigatorio"));
            return;
        }
        if (!scenarioKeys.Add(scenario.Key))
            errors.Add(new BridgeValidationIssue("scenario.key.duplicate", $"$.scenarios[{scenario.Key}]", $"cenario '{scenario.Key}' duplicado"));

        if (string.IsNullOrWhiteSpace(scenario.Prompt))
            errors.Add(new BridgeValidationIssue("scenario.prompt.empty", $"$.scenarios[{scenario.Key}].prompt", "prompt obrigatorio"));

        if (!BridgeManifestRules.ActionModes.Contains(scenario.Tools.Mode))
            errors.Add(new BridgeValidationIssue("scenario.tools.mode.invalid", $"$.scenarios[{scenario.Key}].tools.mode", $"tools.mode '{scenario.Tools.Mode}' invalido"));

        if (scenario.Loop.MaxTurns < 1)
            errors.Add(new BridgeValidationIssue("scenario.loop.maxTurns.invalid", $"$.scenarios[{scenario.Key}].loop.maxTurns", "maxTurns deve ser >= 1"));
        if (scenario.Loop.MaxNudges < 0)
            errors.Add(new BridgeValidationIssue("scenario.loop.maxNudges.invalid", $"$.scenarios[{scenario.Key}].loop.maxNudges", "maxNudges nao pode ser negativo"));

        foreach (var assertion in scenario.Assertions)
        {
            if (!BridgeManifestRules.AssertionTypes.Contains(assertion.Type))
                errors.Add(new BridgeValidationIssue("scenario.assertion.unknown", $"$.scenarios[{scenario.Key}].assertions", $"assertion '{assertion.Type}' desconhecida"));
            if (assertion.Value is < 0)
                errors.Add(new BridgeValidationIssue("scenario.assertion.value.invalid", $"$.scenarios[{scenario.Key}].assertions", "valor de assertion nao pode ser negativo"));
        }

        foreach (var token in FindPlaceholders(scenario.Prompt).Concat(FindPlaceholders(scenario.SystemPrompt)))
        {
            if (!BridgeManifestRules.Placeholders.Contains(token))
                warnings.Add(new BridgeValidationIssue("scenario.placeholder.unknown", $"$.scenarios[{scenario.Key}]", $"placeholder '{{{token}}}' desconhecido"));
        }
    }

    private static IEnumerable<string> FindPlaceholders(string? text)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        foreach (Match match in PlaceholderRegex.Matches(text))
            yield return match.Groups[1].Value;
    }

    private static bool IsHttpUrl(string? url, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(url))
        {
            error = "url obrigatoria";
            return false;
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            error = $"url '{url}' nao e http(s) absoluta";
            return false;
        }
        return true;
    }
}
