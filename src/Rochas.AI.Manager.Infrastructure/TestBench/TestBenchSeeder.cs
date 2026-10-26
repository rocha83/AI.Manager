using System.Text.Json;
using Microsoft.Data.Sqlite;
using Rochas.AI.Manager.Bridge.Contracts;
using Rochas.AI.Manager.Infrastructure.Data;

namespace Rochas.AI.Manager.Infrastructure.TestBench;

/// <summary>Resultado de um seed do TestBench (idempotente por commit + hash).</summary>
/// <param name="Inserted">Falso quando o plano ja existia (no-op).</param>
/// <param name="PlanId">Id do plano no banco.</param>
/// <param name="ManifestHash">Hash canonico do manifesto.</param>
/// <param name="Scenarios">Cenarios gravados nesta chamada.</param>
/// <param name="Runners">Perfis de runner gravados nesta chamada.</param>
public sealed record TestBenchSeedResult(
    bool Inserted,
    int PlanId,
    string ManifestHash,
    int Scenarios,
    int Runners);

/// <summary>
/// Persiste um baseline de testes como DADOS do workflow: plano versionado,
/// stages/steps, cenarios e perfis de runner. A chave natural e
/// <c>(source_commit, manifest_hash)</c>; reexecutar o seed nao duplica nem
/// sobrescreve um plano ja aprovado (F2 do roadmap).
/// </summary>
public static class TestBenchSeeder
{
    /// <summary>Carrega o seed do disco e persiste (db default do AppDb).</summary>
    public static TestBenchSeedResult SeedFile(string baselinePath, string? dbPath = null)
        => Seed(WarBaselineReader.Load(baselinePath), dbPath);

    /// <summary>Persiste um baseline ja parseado/validado.</summary>
    /// <param name="baseline">Envelope com manifesto e runners.</param>
    /// <param name="dbPath">Caminho do SQLite; nulo usa o default do <see cref="AppDb"/>.</param>
    public static TestBenchSeedResult Seed(BridgeBaseline baseline, string? dbPath = null)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        var manifest = baseline.Manifest
            ?? throw new InvalidOperationException("baseline sem manifesto");

        string hash = string.IsNullOrWhiteSpace(manifest.Experiment.ManifestHash)
            ? ManifestCanonicalizer.ComputeManifestHash(manifest)
            : manifest.Experiment.ManifestHash;
        manifest.Experiment.ManifestHash = hash;

        string connectionString = string.IsNullOrWhiteSpace(dbPath)
            ? InitDefault()
            : AppDb.EnsureDatabase(dbPath!);

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        // Idempotencia ANTES de qualquer escrita: commit + hash identifica o plano.
        using (var find = connection.CreateCommand())
        {
            find.CommandText = """
                SELECT id FROM experiment_plans
                 WHERE source_commit = $commit AND manifest_hash = $hash
                 LIMIT 1
                """;
            find.Parameters.AddWithValue("$commit", baseline.SourceCommit ?? string.Empty);
            find.Parameters.AddWithValue("$hash", hash);
            object? existing = find.ExecuteScalar();
            if (existing is not null and not DBNull)
                return new TestBenchSeedResult(false, Convert.ToInt32(existing), hash, 0, 0);
        }

        string manifestJson = BridgeBaselineCodec.SerializeManifest(manifest);
        int planId;
        try
        {
            using var transaction = connection.BeginTransaction();

            planId = InsertPlan(connection, transaction, baseline, manifest, hash, manifestJson);
            int stages = InsertStages(connection, transaction, planId, manifest);
            int scenarios = InsertScenarios(connection, transaction, planId, manifest);
            int runners = InsertRunners(connection, transaction, planId, baseline.Runners);

            using (var update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText =
                    "UPDATE experiment_plans SET code = $code, name = $name WHERE id = $id";
                update.Parameters.AddWithValue("$code", $"plan-{planId}");
                update.Parameters.AddWithValue("$name", manifest.Experiment.Name);
                update.Parameters.AddWithValue("$id", planId);
                update.ExecuteNonQuery();
            }

            transaction.Commit();
            _ = stages;
            return new TestBenchSeedResult(true, planId, hash, scenarios, runners);
        }
        catch
        {
            // Sem transacao pendente: o plano parcial nao fica no banco.
            throw;
        }
    }

    private static string InitDefault()
    {
        AppDb.Init();
        return AppDb.ConnectionString;
    }

    private static int InsertPlan(
        SqliteConnection connection,
        SqliteTransaction transaction,
        BridgeBaseline baseline,
        BridgeManifest manifest,
        string hash,
        string manifestJson)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO experiment_plans
                (parent_id, code, name, is_active, created_at, experiment_id,
                 source_commit, manifest_hash, manifest_json, status, statement, target)
            VALUES
                (NULL, NULL, $name, 1, $created, $experiment,
                 $commit, $hash, $json, 'Draft', $statement, $target);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$name", manifest.Experiment.Name);
        cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$experiment", manifest.Experiment.Id);
        cmd.Parameters.AddWithValue("$commit", baseline.SourceCommit ?? string.Empty);
        cmd.Parameters.AddWithValue("$hash", hash);
        cmd.Parameters.AddWithValue("$json", manifestJson);
        cmd.Parameters.AddWithValue("$statement", (object?)manifest.Experiment.Statement ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$target", manifest.Target.IsGate ? "q4" : "q3");
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static int InsertStages(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int planId,
        BridgeManifest manifest)
    {
        int count = 0;
        int order = 0;
        foreach (var stage in manifest.Stages)
        {
            int stageId;
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = """
                    INSERT INTO circuit_stages
                        (parent_id, code, name, is_active, created_at, plan_id, key, title,
                         sort_order, max_parallel_steps, on_failure, gate_policy_json)
                    VALUES
                        (NULL, $code, $name, 1, $created, $plan, $key, $title,
                         $order, $parallel, $failure, $gate);
                    SELECT last_insert_rowid();
                    """;
                cmd.Parameters.AddWithValue("$code", stage.Key);
                cmd.Parameters.AddWithValue("$name", stage.Title ?? stage.Key);
                cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
                cmd.Parameters.AddWithValue("$plan", planId);
                cmd.Parameters.AddWithValue("$key", stage.Key);
                cmd.Parameters.AddWithValue("$title", (object?)stage.Title ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$order", ++order);
                cmd.Parameters.AddWithValue("$parallel", Math.Max(1, stage.MaxParallelSteps));
                cmd.Parameters.AddWithValue("$failure", stage.OnFailure);
                cmd.Parameters.AddWithValue("$gate", JsonSerializer.Serialize(stage.Gate));
                stageId = Convert.ToInt32(cmd.ExecuteScalar());
            }
            count++;

            int stepOrder = 0;
            foreach (var step in stage.Steps)
            {
                using var stepCmd = connection.CreateCommand();
                stepCmd.Transaction = transaction;
                stepCmd.CommandText = """
                    INSERT INTO circuit_steps
                        (parent_id, code, name, is_active, created_at, stage_id, key, kind,
                         command, args_json, env_json, timeout_sec, depends_on_json,
                         scenario_selector_json, verdict_parser, sort_order)
                    VALUES
                        (NULL, $code, $name, 1, $created, $stage, $key, $kind,
                         $command, $args, $env, $timeout, $depends,
                         $selector, $parser, $order)
                    """;
                stepCmd.Parameters.AddWithValue("$code", $"{stage.Key}.{step.Key}");
                stepCmd.Parameters.AddWithValue("$name", step.Key);
                stepCmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
                stepCmd.Parameters.AddWithValue("$stage", stageId);
                stepCmd.Parameters.AddWithValue("$key", step.Key);
                stepCmd.Parameters.AddWithValue("$kind", step.Kind);
                stepCmd.Parameters.AddWithValue("$command", (object?)step.Command ?? DBNull.Value);
                stepCmd.Parameters.AddWithValue("$args", JsonSerializer.Serialize(step.Args));
                stepCmd.Parameters.AddWithValue("$env", JsonSerializer.Serialize(step.Env));
                stepCmd.Parameters.AddWithValue("$timeout", step.TimeoutSec);
                stepCmd.Parameters.AddWithValue("$depends", JsonSerializer.Serialize(step.DependsOn));
                stepCmd.Parameters.AddWithValue("$selector", JsonSerializer.Serialize(step.ScenarioSelector));
                stepCmd.Parameters.AddWithValue("$parser", (object?)step.VerdictParser ?? DBNull.Value);
                stepCmd.Parameters.AddWithValue("$order", ++stepOrder);
                stepCmd.ExecuteNonQuery();
                count++;
            }
        }
        return count;
    }

    private static int InsertScenarios(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int planId,
        BridgeManifest manifest)
    {
        int count = 0;
        foreach (var scenario in manifest.Scenarios)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO test_scenarios
                    (parent_id, code, name, is_active, created_at, plan_id, key, title,
                     prompt, system_prompt, workspace_json, model_json, tools_json,
                     loop_json, assertions_json, tags, weight)
                VALUES
                    (NULL, $code, $name, 1, $created, $plan, $key, $title,
                     $prompt, $system, $workspace, $model, $tools, $loop,
                     $assertions, $tags, $weight)
                """;
            cmd.Parameters.AddWithValue("$code", scenario.Key);
            cmd.Parameters.AddWithValue("$name", scenario.Title ?? scenario.Key);
            cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("$plan", planId);
            cmd.Parameters.AddWithValue("$key", scenario.Key);
            cmd.Parameters.AddWithValue("$title", (object?)scenario.Title ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$prompt", scenario.Prompt);
            cmd.Parameters.AddWithValue("$system", (object?)scenario.SystemPrompt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$workspace", JsonSerializer.Serialize(scenario.Workspace));
            cmd.Parameters.AddWithValue("$model", JsonSerializer.Serialize(scenario.Model));
            cmd.Parameters.AddWithValue("$tools", JsonSerializer.Serialize(scenario.Tools));
            cmd.Parameters.AddWithValue("$loop", JsonSerializer.Serialize(scenario.Loop));
            cmd.Parameters.AddWithValue("$assertions", JsonSerializer.Serialize(scenario.Assertions));
            cmd.Parameters.AddWithValue("$tags", scenario.Tags is { Count: > 0 }
                ? string.Join(',', scenario.Tags)
                : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$weight", Math.Max(1, scenario.Weight));
            cmd.ExecuteNonQuery();
            count++;
        }
        return count;
    }

    private static int InsertRunners(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int planId,
        IReadOnlyList<BridgeRunnerProfile> runners)
    {
        int count = 0;
        foreach (var runner in runners)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO runner_profiles
                    (parent_id, code, name, is_active, created_at, runner_id, bridge_url,
                     upstream_url, model_id, context_window, max_tokens, is_gate,
                     is_mirror, capabilities_json)
                VALUES
                    (NULL, $code, $name, 1, $created, $runner, $bridge,
                     $upstream, $model, $context, $max, $gate, $mirror, $caps)
                """;
            cmd.Parameters.AddWithValue("$code", $"{planId}.{runner.Id}");
            cmd.Parameters.AddWithValue("$name", runner.Name);
            cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("$runner", runner.Id);
            cmd.Parameters.AddWithValue("$bridge", runner.BridgeUrl);
            cmd.Parameters.AddWithValue("$upstream", runner.UpstreamUrl);
            cmd.Parameters.AddWithValue("$model", runner.ModelId);
            cmd.Parameters.AddWithValue("$context", runner.ContextWindow);
            cmd.Parameters.AddWithValue("$max", runner.MaxTokens);
            cmd.Parameters.AddWithValue("$gate", runner.IsGate ? 1 : 0);
            cmd.Parameters.AddWithValue("$mirror", runner.IsMirror ? 1 : 0);
            cmd.Parameters.AddWithValue("$caps", JsonSerializer.Serialize(runner.Capabilities));
            cmd.ExecuteNonQuery();
            count++;
        }
        return count;
    }
}
