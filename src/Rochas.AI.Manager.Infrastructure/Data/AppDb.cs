using Microsoft.Data.Sqlite;
using System.IO;

namespace Rochas.AI.Manager.Infrastructure.Data;

public static class AppDb
{
    private static string _dbPath = "";
    public static string Path { get => _dbPath; private set => _dbPath = value; }
    public static string ConnectionString => $"Data Source={_dbPath};Cache=Shared";

    public static void Init(string? path = null)
    {
        Path = path ?? System.IO.Path.Combine(AppContext.BaseDirectory, "ai.manager.db");
        EnsureDatabase(Path);
    }

    /// <summary>
    /// Cria/atualiza o schema em um arquivo e devolve a connection string, SEM
    /// trocar o banco global. Usado pelo TestBench (db temporario) e por testes
    /// que nao podem mutar <see cref="Path"/>.
    /// </summary>
    public static string EnsureDatabase(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("caminho do banco obrigatorio", nameof(path));

        var dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        string connectionString = $"Data Source={path};Cache=Shared";
        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using (var prag = conn.CreateCommand())
        {
            prag.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=10000;";
            prag.ExecuteNonQuery();
        }
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS users (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, name TEXT NOT NULL, is_active INTEGER NOT NULL DEFAULT 1, email TEXT NOT NULL, password_hash TEXT NOT NULL, is_admin INTEGER NOT NULL DEFAULT 0, created_at TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS agents (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, name TEXT NOT NULL, is_active INTEGER NOT NULL DEFAULT 1, model TEXT NOT NULL, bridge_url TEXT NOT NULL, temperature REAL NOT NULL DEFAULT 0.4, thinking TEXT NOT NULL DEFAULT 'off', system_prompt TEXT NOT NULL DEFAULT '', mode TEXT NOT NULL DEFAULT 'Build', tools TEXT NOT NULL DEFAULT '', created_at TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS model_artifacts (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, is_active INTEGER NOT NULL DEFAULT 1, name TEXT NOT NULL, family TEXT NOT NULL, variant TEXT, format TEXT NOT NULL, file_path TEXT, file_hash TEXT, is_trainable INTEGER NOT NULL DEFAULT 0, status TEXT NOT NULL DEFAULT 'Draft', created_at TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS datasets (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, is_active INTEGER NOT NULL DEFAULT 1, name TEXT NOT NULL, format TEXT NOT NULL, version TEXT, file_path TEXT, file_hash TEXT, is_validated INTEGER NOT NULL DEFAULT 0, created_at TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS instruction_profiles (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, is_active INTEGER NOT NULL DEFAULT 1, name TEXT NOT NULL, description TEXT, instructions_json TEXT, version TEXT, status TEXT NOT NULL DEFAULT 'Draft', created_at TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS training_patterns (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, is_active INTEGER NOT NULL DEFAULT 1, name TEXT NOT NULL, profile_id INTEGER, model_artifact_id INTEGER, pattern_json TEXT, created_at TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS experiments (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, is_active INTEGER NOT NULL DEFAULT 1, name TEXT NOT NULL, hypothesis TEXT, base_artifact_id INTEGER, tuned_artifact_id INTEGER, metrics_json TEXT, status TEXT NOT NULL DEFAULT 'Planned', created_at TEXT NOT NULL, rejection_reason TEXT);
            CREATE TABLE IF NOT EXISTS training_jobs (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, name TEXT NOT NULL DEFAULT '', is_active INTEGER NOT NULL DEFAULT 1, status TEXT NOT NULL DEFAULT 'Queued', model_artifact_id INTEGER, dataset_id INTEGER, experiment_id INTEGER, profile_name TEXT, advanced_mode INTEGER NOT NULL DEFAULT 0, training_method TEXT NOT NULL DEFAULT 'SFT', configuration_json TEXT, created_at TEXT NOT NULL, started_at TEXT, completed_at TEXT, error_message TEXT, last_loss REAL, vram_used_mb REAL, epochs_completed INTEGER NOT NULL DEFAULT 0, best_accuracy REAL, throughput_tokens_per_sec REAL NOT NULL DEFAULT 0);
            CREATE TABLE IF NOT EXISTS harness_profiles (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, is_active INTEGER NOT NULL DEFAULT 1, name TEXT NOT NULL, description TEXT, status TEXT NOT NULL DEFAULT 'Draft', created_at TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS harness_profile_versions (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, name TEXT NOT NULL DEFAULT '', is_active INTEGER NOT NULL DEFAULT 1, profile_id INTEGER NOT NULL, version INTEGER NOT NULL, mode TEXT NOT NULL DEFAULT 'ClientNative', checksum TEXT, approved_by TEXT, approved_at TEXT, created_at TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS harness_attachments (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, name TEXT NOT NULL DEFAULT '', is_active INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL, version_id INTEGER NOT NULL, kind TEXT NOT NULL DEFAULT 'Core', role TEXT NOT NULL DEFAULT 'general', content TEXT NOT NULL DEFAULT '', char_count INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE IF NOT EXISTS tool_policies (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, name TEXT NOT NULL DEFAULT '', is_active INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL, role TEXT NOT NULL DEFAULT 'general', allow TEXT NOT NULL DEFAULT '', deny TEXT NOT NULL DEFAULT '', require_call INTEGER NOT NULL DEFAULT 0, max_turns INTEGER NOT NULL DEFAULT 10, stall_limit INTEGER NOT NULL DEFAULT 3);
            CREATE TABLE IF NOT EXISTS model_bindings (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, name TEXT NOT NULL DEFAULT '', is_active INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL, provider TEXT NOT NULL, model_id TEXT NOT NULL, max_tokens INTEGER NOT NULL DEFAULT 4096, context_window INTEGER NOT NULL DEFAULT 32768, thinking TEXT NOT NULL DEFAULT 'off', temperature REAL NOT NULL DEFAULT 0.5, uses_tool_coercion INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE IF NOT EXISTS harness_runs (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, name TEXT NOT NULL DEFAULT '', is_active INTEGER NOT NULL DEFAULT 1, evidence_id INTEGER NOT NULL, profile_version_id INTEGER NOT NULL, model_binding_id INTEGER NOT NULL, request_hash TEXT, tool_calls INTEGER NOT NULL DEFAULT 0, files_created INTEGER NOT NULL DEFAULT 0, result_code INTEGER NOT NULL DEFAULT 0, duration_ms INTEGER NOT NULL DEFAULT 0, tokens_per_second REAL NOT NULL DEFAULT 0, created_at TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS bridge_captures (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, name TEXT NOT NULL DEFAULT '', is_active INTEGER NOT NULL DEFAULT 1, mode TEXT NOT NULL DEFAULT 'unknown', request_json TEXT, response_json TEXT, request_hash TEXT, created_at TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS training_workflow_cards (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, name TEXT NOT NULL DEFAULT '', is_active INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL, owner_login TEXT NOT NULL DEFAULT '', updated_at TEXT, updater_id INTEGER, last_update TEXT, title TEXT NOT NULL DEFAULT '', description TEXT, experiment_id INTEGER, accuracy REAL, target_accuracy REAL NOT NULL DEFAULT 0.9, status_id INTEGER NOT NULL DEFAULT 1, next_status_id INTEGER, requester_id INTEGER, approver_id INTEGER, request_note TEXT, approval_note TEXT, approved INTEGER NOT NULL DEFAULT 0, rejection_reason TEXT);
            CREATE TABLE IF NOT EXISTS entity_statuses (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, name TEXT NOT NULL DEFAULT '', is_active INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL, description TEXT);
            CREATE TABLE IF NOT EXISTS workflow_steps (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, name TEXT NOT NULL DEFAULT '', is_active INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL, entity_hash_code INTEGER NOT NULL DEFAULT 0, sort_order INTEGER NOT NULL DEFAULT 0, status_id INTEGER NOT NULL DEFAULT 0, next_status_id INTEGER NOT NULL DEFAULT 0, custom_rule TEXT);
            CREATE TABLE IF NOT EXISTS workflow_transitions (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, name TEXT NOT NULL DEFAULT '', is_active INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL, entity_hash_code INTEGER NOT NULL DEFAULT 0, entity_id INTEGER NOT NULL, entity_status_from INTEGER NOT NULL DEFAULT 0, entity_status_to INTEGER NOT NULL DEFAULT 0, action TEXT NOT NULL DEFAULT 'Submit', approver_id INTEGER NOT NULL DEFAULT 0, note TEXT);
            CREATE TABLE IF NOT EXISTS experiment_plans (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, name TEXT NOT NULL DEFAULT '', is_active INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL, experiment_id INTEGER NOT NULL DEFAULT 0, source_commit TEXT NOT NULL DEFAULT '', manifest_hash TEXT NOT NULL DEFAULT '', manifest_json TEXT NOT NULL DEFAULT '', status TEXT NOT NULL DEFAULT 'Draft', statement TEXT, target TEXT NOT NULL DEFAULT 'q4');
            CREATE TABLE IF NOT EXISTS circuit_stages (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, name TEXT NOT NULL DEFAULT '', is_active INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL, plan_id INTEGER NOT NULL DEFAULT 0, key TEXT NOT NULL DEFAULT '', title TEXT, sort_order INTEGER NOT NULL DEFAULT 0, max_parallel_steps INTEGER NOT NULL DEFAULT 1, on_failure TEXT NOT NULL DEFAULT 'continue', gate_policy_json TEXT);
            CREATE TABLE IF NOT EXISTS circuit_steps (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, name TEXT NOT NULL DEFAULT '', is_active INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL, stage_id INTEGER NOT NULL DEFAULT 0, key TEXT NOT NULL DEFAULT '', kind TEXT NOT NULL DEFAULT 'native', command TEXT, args_json TEXT, env_json TEXT, timeout_sec INTEGER NOT NULL DEFAULT 0, depends_on_json TEXT, scenario_selector_json TEXT, verdict_parser TEXT, sort_order INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE IF NOT EXISTS test_scenarios (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, name TEXT NOT NULL DEFAULT '', is_active INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL, plan_id INTEGER NOT NULL DEFAULT 0, key TEXT NOT NULL DEFAULT '', title TEXT, prompt TEXT NOT NULL DEFAULT '', system_prompt TEXT, workspace_json TEXT, model_json TEXT, tools_json TEXT, loop_json TEXT, assertions_json TEXT, tags TEXT, weight INTEGER NOT NULL DEFAULT 1);
            CREATE TABLE IF NOT EXISTS runner_profiles (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, name TEXT NOT NULL DEFAULT '', is_active INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL, runner_id TEXT NOT NULL DEFAULT 'q4', bridge_url TEXT NOT NULL DEFAULT '', upstream_url TEXT NOT NULL DEFAULT '', model_id TEXT NOT NULL DEFAULT '', context_window INTEGER NOT NULL DEFAULT 0, max_tokens INTEGER NOT NULL DEFAULT 0, is_gate INTEGER NOT NULL DEFAULT 1, is_mirror INTEGER NOT NULL DEFAULT 0, capabilities_json TEXT);
            CREATE TABLE IF NOT EXISTS test_runs (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, name TEXT NOT NULL DEFAULT '', is_active INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL, plan_id INTEGER NOT NULL DEFAULT 0, status TEXT NOT NULL DEFAULT 'Running', gate TEXT NOT NULL DEFAULT 'Pending', started_at TEXT, finished_at TEXT, result_json TEXT, evidence_hash TEXT);
            CREATE TABLE IF NOT EXISTS test_stage_runs (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, name TEXT NOT NULL DEFAULT '', is_active INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL, run_id INTEGER NOT NULL DEFAULT 0, stage_key TEXT NOT NULL DEFAULT '', verdict TEXT NOT NULL DEFAULT 'SKIP', seconds REAL NOT NULL DEFAULT 0, cause TEXT);
            CREATE TABLE IF NOT EXISTS test_step_runs (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, name TEXT NOT NULL DEFAULT '', is_active INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL, run_id INTEGER NOT NULL DEFAULT 0, stage_key TEXT NOT NULL DEFAULT '', step_key TEXT NOT NULL DEFAULT '', verdict TEXT NOT NULL DEFAULT 'SKIP', seconds REAL NOT NULL DEFAULT 0, exit_code INTEGER, cause TEXT);
            CREATE TABLE IF NOT EXISTS test_scenario_runs (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, name TEXT NOT NULL DEFAULT '', is_active INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL, run_id INTEGER NOT NULL DEFAULT 0, scenario_key TEXT NOT NULL DEFAULT '', verdict TEXT NOT NULL DEFAULT 'SKIP', seconds REAL NOT NULL DEFAULT 0, files_created INTEGER NOT NULL DEFAULT 0, total_chars INTEGER NOT NULL DEFAULT 0, detail TEXT);
            CREATE TABLE IF NOT EXISTS test_evidence (id INTEGER PRIMARY KEY AUTOINCREMENT, parent_id INTEGER, code TEXT, name TEXT NOT NULL DEFAULT '', is_active INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL, run_id INTEGER NOT NULL DEFAULT 0, kind TEXT NOT NULL DEFAULT 'log', path TEXT NOT NULL DEFAULT '', sha256 TEXT NOT NULL DEFAULT '', bytes INTEGER NOT NULL DEFAULT 0);
            CREATE INDEX IF NOT EXISTS ix_experiment_plans_key ON experiment_plans(source_commit, manifest_hash);
            CREATE INDEX IF NOT EXISTS ix_circuit_stages_plan ON circuit_stages(plan_id);
            CREATE INDEX IF NOT EXISTS ix_circuit_steps_stage ON circuit_steps(stage_id);
            CREATE INDEX IF NOT EXISTS ix_test_scenarios_plan ON test_scenarios(plan_id);
            CREATE INDEX IF NOT EXISTS ix_test_runs_plan ON test_runs(plan_id);
            CREATE INDEX IF NOT EXISTS ix_test_steps_run ON test_step_runs(run_id);
            CREATE INDEX IF NOT EXISTS ix_test_scenarios_run ON test_scenario_runs(run_id);
            """;
        cmd.ExecuteNonQuery();
        // Migracao: tipo/enunciado do experimento (Training vs Test) — F2.
        try
        {
            using var alterKind = conn.CreateCommand();
            alterKind.CommandText = "ALTER TABLE experiments ADD COLUMN kind TEXT NOT NULL DEFAULT 'Training';";
            alterKind.ExecuteNonQuery();
        }
        catch { /* coluna ja existe */ }
        try
        {
            using var alterStatement = conn.CreateCommand();
            alterStatement.CommandText = "ALTER TABLE experiments ADD COLUMN statement TEXT;";
            alterStatement.ExecuteNonQuery();
        }
        catch { /* coluna ja existe */ }
        // Migracao: garantir coluna training_method em DBs antigos
        try
        {
            using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE training_jobs ADD COLUMN training_method TEXT NOT NULL DEFAULT 'SFT';";
            alter.ExecuteNonQuery();
        }
        catch { /* coluna ja existe */ }
        // Migracao: flag de coercao de tools no cadastro de modelos
        try
        {
            using var alterCoercion = conn.CreateCommand();
            alterCoercion.CommandText = "ALTER TABLE model_bindings ADD COLUMN uses_tool_coercion INTEGER NOT NULL DEFAULT 0;";
            alterCoercion.ExecuteNonQuery();
        }
        catch { /* coluna ja existe */ }

        return connectionString;
    }
}
