# F1 e F2 — status da implementação (2026-10-26; atualizado em 2026-10-30)

> **Nota de nomenclatura (2026-10-30):** este documento mantém os rótulos do corte `b01ac19`; no `ROADMAP-FIM-MIGRACAO-Q4.md`, **F2** passou a nomear o painel nativo e a entrega de dados descrita aqui é pré-requisito da **F3**.

**Escopo deste corte:** F1 (paridade de artefatos do host) e F2 (enunciados como
dados) do `PLANO-APROXIMACAO-URGENTE.md` / `ROADMAP-FIM-MIGRACAO-Q4.md`,
implementados com testes unitários verdes. Este documento registra o que está
pronto, o que ficou fora e as decisões que precisam ser tomadas antes de seguir.

**Verificação (executada nesta máquina):**

> Atualização 2026-10-30: o anti-repeat semântico/Levenshtein saiu do código e dos testes; a contagem do Bridge passou a refletir apenas a suíte clássica.

```bash
dotnet build Rochas.AI.Manager.slnx -c Release
dotnet test tests/Rochas.AI.Manager.Bridge.Tests -c Release   # 136 passed
dotnet test tests/Rochas.AI.Manager.Tests -c Release           # 105 passed, 1 skip
```

---

## F1 — paridade de artefatos do host

### Entregue

| Artefato | Onde | Estado |
|---|---|---|
| Pacote de tools no host | `src/Rochas.AI.Manager.Bridge/Tools/` | completo |
| `IToolHandler` / `ToolContext` / `ToolResult` | idem | completo |
| `WorkspaceGuard` (jail + raízes extras) | idem | completo |
| `ShellInspector` (negação fail-closed) | idem | completo |
| `ProcessRunner` (timeout + kill tree) | idem | completo |
| Handlers `shell/read/write/edit/grep/glob/fetch/git/subagent` | idem | completo |
| `BridgeToolExecutor` + `BridgeToolRequest/Response` | idem + Contracts | completo |
| Catálogo `ToolCatalog` (shapes chat e Responses) | `Tools/ToolCatalog.cs` | completo |
| `POST /api/tools/execute` | `Bridge.Api` | completo |
| `GET /api/tools` | `Bridge.Api` | completo |
| `GET /api/harness` | `Bridge.Api` | completo |
| `GET /api/metrics` (transporte + tools) | `Bridge.Api` | completo |
| `POST /api/permission/check` | `Bridge.Api` | completo (delega ao inspetor/validador) |
| `POST /api/translate` | `Bridge.Api` | completo |
| `POST /api/sanitize` | `Bridge.Api` | completo |
| `POST /api/convert` | `Bridge.Api` | completo |
| `GET /api/config` | `Bridge.Api` | completo |
| `GET /v1/models` (passthrough do upstream) | `Bridge.Api` | completo |
| Contadores `ToolMetrics` / `BridgeMetrics` | `Bridge/Tools` + `Bridge/Transport` | completo |

### Testes

`tests/Rochas.AI.Manager.Bridge.Tests/ToolHostTests.cs` (21 casos):

- jail do workspace (escape, raiz extra autorizada);
- write → read → edit no disco;
- `edit` recusa trecho inexistente;
- inspetor nega `rm`, `rm -rf`, `bash -c "rm ..."`, `sed -i`, pipe para shell,
  `git reset --hard`;
- inspetor libera `ls`, `grep`, `bash -n`, `dotnet build`;
- `shell` executa leitura real no workspace;
- `git` é somente leitura no role `plan` e grava no `build`;
- contrato HTTP do executor (request → response, duração, metadados);
- aliases `bash` → `shell`;
- shapes do catálogo (chat vs Responses).

A política de `POST /api/permission/check` não duplica regra: `shell`/`bash`
passam por `ShellInspector.Validate` (mesmo fail-closed do executor) e `git` por
`GitToolHandler.Validate` (escrita só em `build`/`exec`, `push`/`reset`/`force`
negados). O role sai do sufixo do modelo (`...-plan`). Coberto por 13 casos
adicionais (34 no total).

### Fora deste corte (F1)

- **Paridade no `Bridge.Runner` (`serve`)**: o `Runner` tem host próprio e ainda
  não monta os endpoints de tools. Decisão necessária: (a) extrair um
  `MapBridgeEndpoints(...)` compartilhado em `Bridge.Hosting` e usar nos dois
  hosts, ou (b) aposentar o `serve` do Runner e manter só o `Bridge.Api`.
- **ONNX/embeddings**: fora de escopo no roadmap atual; não iniciado.
- `POST /api/config` de escrita e hot-reload: só leitura hoje.

---

## F2 — enunciados como dados

### Entregue

| Artefato | Onde | Estado |
|---|---|---|
| `Experiment.Kind` (`Training`/`Test`) e `Experiment.Statement` | Domain + AppDb | completo |
| Entidades do TestBench | `Domain/TestBench/` | completo |
| Schema idempotente (10 tabelas + índices) | `Infrastructure/Data/AppDb.cs` | completo |
| `BridgeBaseline` (procedência + manifesto + runners) | `Bridge.Contracts` | completo |
| `BridgeBaselineCodec` (parse, hash, validação única) | `Bridge.Contracts` | completo |
| `WarBaselineReader` | `Infrastructure/TestBench` | completo |
| `TestBenchSeeder` (idempotente por `commit + hash`) | `Infrastructure/TestBench` | completo |
| `ManifestBuilder` (normaliza, valida, build por runner) | `Application/TestBench` | completo |
| Seed `war-baseline.json` (belts L1 azul/verde) | `Data/Seeds/` | completo |
| `AppDb.EnsureDatabase` (schema sem trocar o banco global) | `Infrastructure/Data` | completo |

Tabelas: `experiment_plans`, `circuit_stages`, `circuit_steps`, `test_scenarios`,
`runner_profiles`, `test_runs`, `test_stage_runs`, `test_step_runs`,
`test_scenario_runs`, `test_evidence`.

### Regras já garantidas

- O seed é **idempotente**: a chave natural é `(source_commit, manifest_hash)`;
  reexecutar não duplica plano, stages, cenários nem runners.
- Plano aprovado **não é sobrescrito** (`status='Approved'` é preservado).
- O hash do manifesto é canônico e **conferido** na leitura; hash divergente
  reprova com mensagem explícita.
- Dois perfis no seed: **Q4 (`:4124`) gate** e **Q3 (`:4125`) espelho**; o
  espelho só entra pós-verde.
- Os belts L1 build (azul/verde) estão no seed como **dados**, com prompt,
  workspace, tools, loop e assertions — nenhuma regra de belt em código.

### Testes

`tests/Rochas.AI.Manager.Tests/TestBench/TestBenchSeedTests.cs` (8 casos):

- seed valida o manifesto e calcula o hash;
- seed idempotente (contagem exata de linhas em 2 execuções);
- plano aprovado não é sobrescrito;
- `BuildForRunner` troca target Q4/Q3 e mantém cenários, com hashes distintos;
- hash divergente reprova;
- manifesto inválido reprova no codec;
- `Experiment.Kind/Statement` persistem;
- `AppDb` cria as 10 tabelas do TestBench.

### Fora deste corte (F2)

- **UI** (telas de plano, cenários, runs e evidências) — não iniciada.
- **Executor de cenário** (rodar o `BridgeScenario` contra o modelo e aplicar
  assertions) — hoje existe o contrato e o seed; falta o loop que consome
  `test_scenarios` e grava `test_*_runs`/`test_evidence`.
- **Wiring com o `Bridge.Runner`** (`manifest`/`serve`) e disparo a partir do
  AI.Manager.
- **Espelho Q3 automático** pós-verde (policy, não só perfil).
- **Import do baseline por commit** (`sourceCommit` hoje é um rótulo fixo).

---

## Decisões para a discussão

1. **Dono da execução**: o loop de cenário roda no processo do AI.Manager
   (chamando `Bridge.Client`) ou dentro do `Bridge.Runner` (que hoje só faz
   dry-run de manifesto)? A recomendação é o Runner executar e o AI.Manager só
   persistir evidência — mantém o executor sem acesso ao banco.
2. **Evidência**: `test_evidence` guarda path + SHA-256; precisamos definir
   retenção (por run), storage (`logs/` do projeto vs artefato no DB) e se o
   log bruto do modelo entra ou só o veredito + métricas.
3. **Gate**: manter `PASS > 0 && FAIL == 0 && STALE == 0` como política default
   e exigir cobertura mínima por suíte? O seed atual tem 2 cenários; a WAR tem
   muito mais.
4. **Q3**: espelho automático logo após o verde do Q4 ou manual (botão)?
5. **Runner `serve`**: unificar endpoints no `Bridge.Api` (recomendado) ou
   duplicar no Runner?

---

## Próximo corte sugerido

1. `MapBridgeEndpoints` compartilhado (fecha a paridade F1 no Runner).
2. Executor de cenário mínimo (`BridgeScenario` → modelo → tools → assertions)
   com 1 teste de integração opt-in.
3. Persistência de `test_runs` + `test_evidence` a partir do executor.
4. Tela de planos/cenários lendo `test_scenarios` (fecha o ciclo "dados, não
   código").
