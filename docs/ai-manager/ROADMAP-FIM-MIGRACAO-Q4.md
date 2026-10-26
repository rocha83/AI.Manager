# Roadmap — fim da migração e go-live do fluxo de testes/treinamento no Q4

**Data:** 2026-10-26  
**Premissa:** o AI.Manager é o concentrador de treinamento e testes; o OpenCodeBridge é doador e será arquivado após paridade. O gate é o **Q4 (:4124)**; o Q3 (:4125) é espelho pós-green. Testes e enunciados são **dados do workflow**, não código hardcoded. Nenhum PASS por afrouxamento de assertiva, troca de instância ou redução de contexto.

**Estado de partida (medido nesta migração):**
- `Rochas.AI.Manager.Bridge` já tem protocolo Responses/chat, SSE, harness, breaker, shim, contexto e anti-repeat.
- Já existem `Bridge.Contracts`, `Bridge.Client`, `Bridge.Hosting`, `Bridge.Api` e `Bridge.Runner` (`serve` + `manifest` dry-run).
- Ainda faltam: execução de tools no host, paridade de endpoints/arquivos do Core, executor de cenário, persistência TestBench e seed dos pipes completos.
- Parcial iniciada: anti-repeat **semântico** opt-in (`AntiRepeatSemantic=false` por padrão; não existia no OCB) e linha de workspace no `MainHarness`. Build local bloqueado: o host tem SDK .NET 9 e a solução exige .NET 10.

**Meta final:** o AI.Manager cria, versiona, executa e registra os pipes L1/L2/L3 no Q4, com evidência e gate, e o resultado alimenta o fluxo de treinamento — tudo sem depender do runtime do OpenCodeBridge.

---

## Visão das 5 fases

| Fase | Foco | Duração alvo | Gate de saída |
|---|---|---|---|
| **F1** | Paridade de artefatos do Bridge | 3–4 dias | host do Bridge executa tools e responde aos endpoints essenciais; build/testes verdes |
| **F2** | TestBench como dados no Manager | 4–5 dias | `war-baseline.json` importado e versionado; manifesto gerado e validado |
| **F3** | Execução dos pipes no Q4 | 5–7 dias | L1 completo verde no Q4 (belts azul/verde + units + devpipe) com evidência |
| **F4** | Cobertura L2/L3 e treino | 4–5 dias | roxa, marrom, preta, tools/dual/mission/science e experimento de treino ligados ao resultado |
| **F5** | Cutover e go-live | 3–4 dias | 3 gates Q4 verdes consecutivos, Q3 espelho sem divergência crítica, OCB congelado |

**Duração total estimada:** 19–25 dias úteis, em ondas únicas e fail-fast (sem ondas paralelas).

---

## F1 — Paridade de artefatos do Bridge (3–4 dias)

**Objetivo:** fechar o que falta para o Bridge ser executável e observável como host de teste, sem depender de `Application`/`Domain`.

**Entregas:**
1. **Tools no host**: expor `POST /api/tools/execute` no `Bridge.Api`/`Bridge.Runner`, com o contrato `BridgeToolRequest/Response`.
2. **Núcleo de tools portado**: mover/portar `ToolContext` (Role, ExtraRoots, Log), `ShellInspector` (payload/pipes), `WorkspaceGuard` (raízes extras) e `GitToolHandler` (write por role) para o projeto compartilhado do Bridge.
3. **Paridade de endpoints**: `/api/harness`, `/api/metrics`, `/api/permission/check`, `/api/translate`, `/api/sanitize`, `/api/convert`, `/api/config`, `/v1/models`; `/api/auth/login` só se o fluxo de teste exigir.
4. **Anti-repeat**: preservar a paridade do legado (janela de igualdade + 3 blocos similares) como default; a sonda **semântica** (`thinking` + `content`) fica **opt-in** (`AntiRepeatSemantic=false` por padrão) e só entra no gate após medição com zero falso-positivo. Concluir retry/abort e telemetria.
5. **Harness**: base enxuta com “use o diretório atual como espaço de trabalho”; tails sem sobreposição no modo `opencode`.
6. **Spike ONNX (opcional, atrás de flag)**: comparar a similaridade lexical com um encoder ONNX pequeno (`multilingual-e5-small` int8, ~113–118 MB, MIT, 384d) para reduzir falso-positivo/negativo. Regras: modelo fora do pacote do Bridge, download com SHA-256, inferência só na “zona cinzenta” lexical, limite de latência e fail-open. Só entra no gate se p95 < 30 ms e zero falso-positivo na suíte de prosa normal.

**Arquivos-chave:**
- `src/Rochas.AI.Manager.Bridge/Transport/BridgeProxy.cs`
- `src/Rochas.AI.Manager.Bridge/AntiRepeat.cs`, `ContentAntiRepeat.cs`, `ResponsesStream.cs`
- `src/Rochas.AI.Manager.Bridge/Flow/`
- `src/Rochas.AI.Manager.Bridge.Api/Program.cs`
- `src/Rochas.AI.Manager.Bridge.Runner/Program.cs`
- `tests/Rochas.AI.Manager.Bridge.Tests/`

**Aceite:**
- `dotnet build Rochas.AI.Manager.slnx -c Release` 0 erros / 0 warnings.
- `Bridge.Tests` 100% verdes (incluindo anti-repeat semântico e `/api/tools/execute`).
- Um `write` remoto via `/api/tools/execute` grava no workspace e respeita o jail.
- Nenhum endpoint novo quebra o protocolo `/v1/*`.

**Riscos/mitigação:**
- Big-bang no host → portar por endpoint, com teste de contrato antes de ligar no fluxo.
- Falso-positivo do anti-repeat semântico → limiar conservador + teste com prosa técnica normal; logar `RepeatSemantic` para auditoria.

---

## F2 — TestBench como dados no Manager (4–5 dias)

**Objetivo:** transformar belts, tools, science e configurações de runner em dados versionados e gerar o manifesto imutável que o Bridge executa.

**Entregas:**
1. **Schema TestBench** no `AppDb` (idempotente): `experiment_plans`, `circuit_stages`, `circuit_steps`, `test_scenarios`, `test_runs`, `test_stage_runs`, `test_step_runs`, `test_scenario_runs`, `test_evidence`, `runner_profiles`.
2. **Estender `Experiment`** com `Kind` (`Training`/`Test`), `Statement` e vínculo ao workflow existente; reusar `TrainingWorkflowCard`.
3. **Seeder WAR** (`Data/Seeds/war-baseline.json`): L1/L2/L3, belts azul/verde/roxa/marrom/preta (build e orch), tools-full, dual-tools, mission, responses, science GPU/CPU e Web. Cada item com prompt, system, tools, loop e assertions extraídos do OCB como oráculo.
4. **Seeder idempotente** por `sourceCommit + manifestHash`; cria `Draft`, nunca sobrescreve `Approved`.
5. **ManifestBuilder/Canonicalizer**: monta e valida o manifesto a partir do plano; hash SHA-256 estável.
6. **RunnerProfiles Q4/Q3**: Q4 como `isGate=true`; Q3 como `isMirror=true` e bloqueado até green.

**Arquivos-chave:**
- `src/Rochas.AI.Manager.Domain/TestBench/`
- `src/Rochas.AI.Manager.Infrastructure/Data/AppDb.cs`
- `src/Rochas.AI.Manager.Infrastructure/TestBench/WarBaselineReader.cs`
- `src/Rochas.AI.Manager.Application/TestBench/ManifestBuilder.cs`
- `src/Rochas.AI.Manager.Web/Data/WarBaselineSeeder.cs`
- `Data/Seeds/war-baseline.json`

**Aceite:**
- Seed roda duas vezes sem duplicar (idempotência provada em teste).
- `war-baseline.json` valida no `BridgeManifestValidator` e o hash confere.
- Manifesto gerado contém todos os cenários com `sourceCommit`, `experiment.version` e `target` Q4.
- Nenhuma regra de negócio do seed vaza para o schema (belts são dados).

**Riscos/mitigação:**
- Seed virar “schema disfarçado” → validar campos genéricos e manter `selector/tags` como dados.
- Divergência de enunciado → congelar o OCB como oráculo e registrar `sourceCommit` em cada seed.

---

## F3 — Execução dos pipes no Q4 (5–7 dias)

**Objetivo:** provar a fatia vertical: o Manager executa L1 no Q4, com tools reais, gate e evidência, sem UI e sem OCB em runtime.

**Entregas:**
1. **Executor de cenário nativo**: loop de turnos (`system + user`), tool `write`/`read`, nudges, stall, review e assertions (`fileCountAtLeast`, `totalCharsAtLeast`, `fileExtensionCountAtLeast`).
2. **Executor de manifesto**: stages/step kind `native` e `command`, timeout, retry, `DependsOn`, `When` mínimo, artefatos e gate.
3. **Comando `run`** no Runner: valida, resolve, executa e grava `BridgeResult` (status, gate, stages, scenarios, métricas, evidência).
4. **Registro no Manager**: `TestRun`/`ScenarioRun`/`Evidence` com hashes e causa de falha; sem lock de SQLite (executor não lê banco).
5. **Piloto L1**: units, devpipe, belts azul/verde (build direto) no Q4; comparar com o legado.

**Arquivos-chave:**
- `src/Rochas.AI.Manager.Bridge/Flow/NativeScenarioExecutor.cs`
- `src/Rochas.AI.Manager.Bridge/Flow/ManifestCircuitTemplate.cs`
- `src/Rochas.AI.Manager.Bridge/Flow/TestCircuitDriver.cs`
- `src/Rochas.AI.Manager.Bridge.Runner/Program.cs`
- `src/Rochas.AI.Manager.Application/TestBench/TestRunService.cs`
- `tests/Rochas.AI.Manager.Bridge.Tests/` e `tests/Rochas.AI.Manager.Tests/TestBench/`

**Aceite:**
- `Bridge.Runner run --manifest war-baseline.json --target q4` produz gate verde no L1.
- Métricas (arquivos/chars) dentro de ±20% do OCB para o mesmo belt.
- Evidência gravada e consultável por `test_run_id`; repetição do run não reusa artefato sujo.
- Nenhum `PASS` quando `FAIL > 0` ou `STALE > 0`.

**Riscos/mitigação:**
- Q4 instável → janela dedicada, pré-flight (`/health`, `/v1/models`) e fail-fast; não reiniciar Q4 sem autorização.
- Custo alto por turno → limitar `maxTurns`, medir tokens/s e usar stall curto.
- `write` repetido → anti-repeat + breaker + nudge único.

---

## F4 — Cobertura L2/L3 e ponte com treino (4–5 dias)

**Objetivo:** fechar a matriz completa do Q4 e ligar o resultado dos testes ao fluxo de treinamento.

**Entregas:**
1. **L2**: tools-full, dual-tools, mission-tools, responses-tools, science GPU/CPU, belts roxa (build/orch).
2. **L3**: belts marrom/preta (build/orch) + Web UI.
3. **Cenários de treino**: SFT/DPO/RM continuam como `Experiment.Kind=Training`; o resultado de teste vira métrica/aceite do card de workflow.
4. **UI mínima**: catálogo de experimentos por `Kind`, detalhe do plano/circuito, runs e evidência; sem editor completo.
5. **Espelho Q3**: habilitar somente após L1–L3 verdes; classificar divergências (infra, bridge, fixture, modelo, assertion).

**Aceite:**
- L2 e L3 verdes no Q4, um nível por vez, com `PASS>0 / FAIL=0 / STALE=0`.
- Um experimento de treino referencia o `TestRun` verde como aceite.
- Q3 espelho sem divergência crítica nos mesmos cenários.
- Falha classificada e rastreável a step/cenário/causa.

**Riscos/mitigação:**
- L2/L3 longos → rodar por suite e por belt, com retomada por `run_id`.
- Web UI frágil → smoke test dedicado antes de incluir no gate.
- Divergência Q3 → não promover; abrir `MirrorDivergence` com dono e ação.

---

## F5 — Cutover e go-live (3–4 dias)

**Objetivo:** tornar o Manager a fonte oficial do fluxo de testes/treinamento no Q4 e congelar o OCB.

**Entregas:**
1. **3 gates Q4 verdes consecutivos** por nível (L1, L2, L3) com o mesmo manifesto/hash.
2. **Regressão final**: Bridge.Tests, Manager.Tests, build 0/0 e smoke do Q4/Q3.
3. **Operação**: health, logs, métricas, retenção de evidência, rollback documentado.
4. **Documentação**: runbook do Q4, catálogo de seeds, como aprovar plano e como ler um gate.
5. **Cutover**: flag `executor=bridge` como default; OCB em modo congelado; arquivamento após janela de segurança.

**Aceite:**
- 3 execuções consecutivas verdes em Q4 para cada nível.
- Nenhuma dependência de runtime do OCB nos pipes oficiais.
- Rollback testado (voltar ao executor legado sem perder evidência).
- Runbook validado por outra pessoa do time.

**Riscos/mitigação:**
- Regressão tardia → manter OCB congelado por 1 semana e comparar gates.
- Perda de evidência → store imutável com SHA-256 e retenção mínima.
- lock SQLite → executor sem DB; API only; WAL/índices.

---

## Sequência obrigatória e regras de execução

1. F1 → F2 → F3 → F4 → F5, **uma onda por vez**.
2. Cada fase só abre a seguinte com o aceite cumprido e registrado.
3. Build + testes verdes antes de qualquer execução no Q4.
4. Q4 é gate; Q3 só depois de green.
5. Falha é fail-fast: parar, classificar, corrigir, reexecutar o mesmo cenário.
6. Nada de UI/espelho antes da fatia vertical provar valor.

## Fora de escopo (não bloqueia o go-live)

- Editor visual completo de circuito.
- Extração do Bridge como repositório/processo separado.
- Dashboards históricos sofisticados.
- Novos modelos além do Q4/E2B já usados nos pipes.

## Métrica de sucesso do roadmap

- **L1, L2 e L3 verdes no Q4**, com evidência e hashes, executados pelo AI.Manager.
- **Zero dependência de runtime do OpenCodeBridge** nos pipes oficiais.
- **Q3 espelho** sem divergência crítica.
- **Um ciclo completo** teste → aceite → treino → avaliação rodando pelo Manager.
