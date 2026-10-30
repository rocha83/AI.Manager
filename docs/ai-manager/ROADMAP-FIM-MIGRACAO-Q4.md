# Roadmap enxuto — absorção do OpenCodeBridge e go-live do fluxo de testes no Q4

**Data:** 2026-10-30
**Baseline do doador:** OpenCodeBridge `8adfda7` (congelado, sem edições).
**Premissa:** o AI.Manager é o concentrador de treinamento e testes; o OpenCodeBridge é o doador congelado, incorporado pelo AI.Manager e continuado como componente independente `Rochas.AI.Manager.Bridge` no GitHub/NuGet. O gate é o **Q4 (:4124)**; o Q3 (:4125) é espelho pós-green. Testes e enunciados são **dados do workflow**, não código hardcoded. Nenhum PASS por afrouxamento de assertiva, troca de instância ou redução de contexto.

**Decisões travadas:**

- O OpenCodeBridge não recebe feature nem correção; todo ajuste vai para o `AI.Manager.Bridge`.
- O OpenCodeBridge **não é descontinuado nem arquivado**: seu conteúdo é incorporado e a continuidade é publicada como `Rochas.AI.Manager.Bridge` (GitHub + NuGet), no padrão dos componentes Rochas.
- As tools saem de `Rochas.AI.Manager.Application/Tools` e passam para `Rochas.AI.Manager.Bridge/Tools`.
- O anti-repeat semântico e a similaridade Levenshtein **saem do escopo e do código**; fica apenas o clássico posicional do OCB.
- O painel operacional é **nativo no AI.Manager** (Razor + partials + AJAX), sem Grafana e sem iframe.
- Este host não tem GPU: o aceite local é build + testes + seed; Q4/Q3 só no host com GPU.
- Q4 é o único gate; Q3 é espelho pós-green e não aprova experimento.

**Estado em `b01ac19` (2026-10-29):**

- F1 entregue em grande parte: tools no host, `/api/tools/execute`, `/api/harness`, `/api/metrics`, `/api/permission/check`, `/api/translate`, `/api/sanitize`, `/api/convert`, `/api/config` e `/v1/models`.
- F2 de dados do TestBench entregue: schema, `war-baseline.json`, `BridgeBaseline`/`Codec` e seeder idempotente. Este roadmap mantém o nome **F2** para o painel nativo; a entrega de dados vira pré-requisito da F3.
- Ainda faltam: `/api/test/*` e ring de telemetria, paridade do `Bridge.Runner`, executor de cenário e painel `/Observation`.

**Neste corte (2026-10-30):**

- `AntiRepeat` voltou à similaridade posicional; semântica/Levenshtein removidos do código e dos testes.
- `MainHarness`/`HarnessTails` sincronizados com o OCB (workspace, data do host, `SYSTEM DO ROLE` e tails).
- Documentação alinhada ao ciclo de vida do OCB (doador congelado, continuidade GitHub/NuGet) e ao painel nativo.
- O dashboard Grafana do OCB (`observation/grafana/dashboards/opencodebridge.json`) é a referência visual e funcional do painel nativo; a especificação exata está em `REPORT-PENDINGS.md` §9.

---

## Visão das 4 fases

| Fase | Foco | Duração alvo | Gate de saída |
|---|---|---|---|
| **F1** | Bridge executável e observável | 4–5 dias | tools rodam no Bridge; endpoints de status/métricas/teste respondem; anti-repeat clássico; build/testes verdes |
| **F2** | Observabilidade nativa | 5–6 dias | `/Observation` com os 15 quadros do Grafana, partials + AJAX 5s, sem iframe/segredo no browser |
| **F3** | TestBench + L1 no Q4 | 6–8 dias | manifesto/seed versionado; executor nativo; L1 verde no Q4 com evidência |
| **F4** | L2/L3, cutover e pacote | 5–6 dias | L2/L3 verdes; Q3 espelho; 3 gates Q4; componente independente publicado |

**Duração total estimada:** 20–25 dias úteis, em uma onda por vez e fail-fast.

---

## F1 — Bridge executável e observável (4–5 dias)

**Objetivo:** fechar o host que o AI.Manager e o painel nativo consomem, sem depender de `Application`/`Domain`.

**Entregas:**

1. **Tools no Bridge**: mover os 16 arquivos de `Application/Tools` para `Bridge/Tools` (namespace `Rochas.AI.Manager.Bridge.Tools`), sincronizar as correções de segurança do OCB `8adfda7` (`ShellInspector`, `ShellToolHandler`, `GitToolHandler`, `WorkspaceGuard`, `ToolCatalog`, `ToolHelp`) e preservar as melhorias locais de `read` (áudio/docx).
2. **API**: `Rochas.AI.Manager.Api` referencia `Bridge` diretamente; `ToolsController`/`Program.cs` usam `Bridge.Tools`; expor `POST /api/tools/execute`.
3. **Anti-repeat clássico**: remover `AntiRepeatSemantic`, `AntiRepeatSemanticForOpencode`, `ResponsesStream.RepeatSemantic`/`lastSemanticProbe`, `AntiRepeat.SemanticDuplicateAt`, a similaridade Levenshtein e os testes semânticos. Restaurar a similaridade **posicional** do OCB (mesmo comprimento, chars iguais na mesma posição) e manter `DefaultNotice` + assinatura canônica de tools.
4. **Harness e modo**: paridade com o OCB em `MainHarness` (linha “use o diretório atual como workspace”), `HarnessTails`, `TailScope`, `main_harness_opencode=false` e `tail_scope`; `--workspace` no runner; status com `main_harness`, `main_harness_scope`, `main_harness_opencode`, `harness_tails`, `tails_managed`, `tails_opencode`, `tail_scope`.
5. **Endpoints de observabilidade**: `/api/status`, `/api/metrics`, `/api/harness`, `/api/test/enunciado`, `/api/test/enunciado/messages`, `/api/test/activity`, `/api/test/probe`, `/api/test/probe/latest`, `/api/test/step`, `/api/test/steps`.
6. **Endpoints de compatibilidade**: `/api/permission/check`, `/api/translate`, `/api/sanitize`, `/api/convert`, `/api/config`, `/v1/models` (quando exigidos pelo fluxo).
7. **Telemetria**: contadores de requests, falhas, modos, tokens/s, arquivos, gates anti-repeat clássicos e tools calls; ring de amostras por step com `ts`, `tps_output`, `tps_total`, `vram_used_mb`, `vram_total_mb`, `gpu_util`, `tools_calls` e `requests`.

**Arquivos-chave:**

- `src/Rochas.AI.Manager.Bridge/Tools/`
- `src/Rochas.AI.Manager.Bridge/AntiRepeat.cs`, `ContentAntiRepeat.cs`, `ResponsesStream.cs`, `BridgeOptions.cs`
- `src/Rochas.AI.Manager.Bridge/Flow/`, `Transport/`, `Harness*`
- `src/Rochas.AI.Manager.Bridge.Api/Program.cs`
- `src/Rochas.AI.Manager.Bridge.Runner/Program.cs`
- `src/Rochas.AI.Manager.Api/Controllers/ToolsController.cs`
- `src/Rochas.AI.Manager.Api/Program.cs`
- `tests/Rochas.AI.Manager.Bridge.Tests/`

**Aceite:**

- `dotnet build Rochas.AI.Manager.slnx -c Release` 0 erros / 0 warnings.
- `Bridge.Tests` e `Manager.Tests` verdes, sem testes semânticos.
- `write` via `/api/tools/execute` grava no workspace e respeita o jail.
- `/api/status`, `/api/metrics`, `/api/harness` e `/api/test/*` respondem com os campos que o painel nativo consome.
- Nenhum projeto referencia `Rochas.OpenCodeBridge.*`.
- Nenhum endpoint novo quebra o protocolo `/v1/*`.

**Riscos/mitigação:**

- Big-bang no host → portar por endpoint, com teste de contrato antes de ligar no fluxo.
- Divergência de segurança nas tools → diff arquivo a arquivo contra o OCB `8adfda7`; testes de jail/role.
- Regressão do anti-repeat → manter os testes clássicos posicionais e remover os semânticos, sem substituir por heurística nova.

---

## F2 — Observabilidade nativa (5–6 dias)

**Objetivo:** substituir o Grafana/iframe por uma página Razor nativa no AI.Manager, com a mesma composição, disposição e leitura operacional do dashboard do OCB.

**Entregas:**

1. Página `/Observation` no menu lateral, protegida por cookie de autenticação.
2. Os **15 quadros** do dashboard `opencodebridge`, na mesma grade de 24 colunas e com o mesmo `x/y/w/h` (detalhe em `REPORT-PENDINGS.md` §9.3).
3. Partials Razor + AJAX a cada 5s; sem recarregar a página, sem iframe, sem Grafana/Infinity/TestData.
4. Cliente server-side para o Bridge (`Bridge:BaseUrl`, `Bridge:ApiKey` por configuração/segredo); o browser nunca recebe bearer.
5. Gráficos em CSS/SVG renderizados no servidor (sem nova biblioteca JS/CDN): barras, bargauges e séries temporais com legenda e downsampling.
6. Estados de indisponibilidade: manter o último snapshot, marcar “degradado” e mostrar a hora da última atualização; não apagar o painel inteiro.
7. Testes de contrato dos campos, renderização dos partials, autenticação, ausência de segredo no HTML e fallback do Bridge.

**Aceite:**

- Os 15 quadros aparecem na mesma posição relativa do Grafana em desktop (≥1200px) e empilham de forma legível em mobile.
- Atualização a cada 5s via partials, sem reload completo e sem iframe.
- Números/tabelas conferem com `/api/metrics`, `/api/status` e `/api/test/*`.
- Nenhum `guest@123`/API key no HTML, JS, logs de request ou resposta ao browser.
- Bridge indisponível não derruba a página; o último snapshot e o horário permanecem.
- Build/testes verdes; sem dependência de Grafana.

**Riscos/mitigação:**

- Endpoint ausente no Bridge → bloquear F2 até F1 expor o contrato; não criar mock permanente.
- Séries longas no browser → limite de 1000 amostras, downsample para ~240 pontos e atualização só com aba visível.
- Layout divergir do Grafana → CSS grid de 24 colunas com `grid-column`/`grid-row` explícitos e tabela de posições no doc.
- Vazamento de credencial → proxy server-side; teste que varre o HTML renderizado.

---

## F3 — TestBench + L1 no Q4 (6–8 dias)

**Objetivo:** transformar belts, tools e configurações em dados versionados, executar L1 no Q4 pelo Bridge e registrar evidência.

**Entregas:**

1. **Schema TestBench** no `AppDb` (idempotente): `experiment_plans`, `circuit_stages`, `circuit_steps`, `test_scenarios`, `test_runs`, `test_stage_runs`, `test_step_runs`, `test_scenario_runs`, `test_evidence`, `runner_profiles`.
2. **`Experiment` + workflow**: `Kind=Training|Test`, `Statement` e vínculo com `TrainingWorkflowCard`; sem `TestExperiment` paralelo.
3. **Seeder WAR**: `Data/Seeds/war-baseline.json` com L1/L2/L3, belts azul/verde/roxa/marrom/preta, tools-full, dual-tools, mission, responses e science; `sourceCommit=8adfda7`; seed idempotente por `sourceCommit + manifestHash`.
4. **ManifestBuilder/Canonicalizer**: manifesto imutável, SHA-256 estável, validação de schema/capabilities.
5. **Executor nativo**: loop de turnos (`system + user`), `write`/`read`, nudges, stall, review e assertions (`fileCountAtLeast`, `totalCharsAtLeast`, `fileExtensionCountAtLeast`).
6. **Runner**: comando `run` que valida, resolve, executa e grava `BridgeResult` (status, gate, stages, scenarios, métricas, evidência).
7. **Piloto L1**: units, devpipe e belts azul/verde no Q4; comparação com o OCB congelado.

**Aceite:**

- Seed roda duas vezes sem duplicar; `war-baseline.json` valida e o hash confere.
- `Bridge.Runner run --manifest war-baseline.json --target q4` produz gate verde no L1.
- Métricas (arquivos/chars) dentro de ±20% do OCB para o mesmo belt.
- Evidência gravada e consultável por `test_run_id`; repetição não reusa artefato sujo.
- Nenhum `PASS` com `FAIL > 0` ou `STALE > 0`.
- Build/testes verdes; nenhuma execução Q4 neste host sem GPU.

**Riscos/mitigação:**

- Q4 instável → janela dedicada, pré-flight (`/health`, `/v1/models`) e fail-fast; não reiniciar Q4 sem autorização.
- Custo por turno → limitar `maxTurns`, medir tokens/s e usar stall curto.
- `write` repetido → anti-repeat clássico + breaker + nudge único.
- Seed virar “schema disfarçado” → campos genéricos; belts são dados.

---

## F4 — L2/L3 e cutover (5–6 dias)

**Objetivo:** fechar a matriz no Q4, rodar o espelho Q3, tornar o Bridge o runtime oficial e publicar o componente independente.

**Entregas:**

1. **L2**: tools-full, dual-tools, mission-tools, responses-tools, science GPU/CPU e belt roxa (build/orch).
2. **L3**: belts marrom/preta (build/orch) + Web UI, um nível por vez.
3. **Espelho Q3**: habilitar somente após Q4 verde do mesmo `manifestHash`; classificar divergências (`infra`, `bridge`, `fixture`, `modelo`, `assertion`) em `MirrorDivergence`.
4. **Cutover**: flag `executor=bridge` default; 3 gates Q4 verdes consecutivos por nível; rollback documentado.
5. **Componente independente**: manter o OCB congelado como referência histórica; remover dependência de seus projetos; publicar `Rochas.AI.Manager.Bridge` no GitHub e no NuGet, com metadados de pacote, README e versionamento SemVer.
6. **Operação**: runbook do Q4, health/logs/métricas, retenção de evidência e leitura do painel nativo.

**Aceite:**

- L2/L3 verdes no Q4 com `PASS>0 / FAIL=0 / STALE=0`.
- Q3 sem divergência crítica; divergência vira item de workflow, nunca gate.
- 3 execuções Q4 verdes consecutivas por nível.
- Rollback testado sem perda de evidência.
- Nenhum runtime oficial depende do OpenCodeBridge.
- `Rochas.AI.Manager.Bridge` instalável via NuGet e repositório GitHub público/interno, sem depender do repo doador.

**Riscos/mitigação:**

- Regressão tardia → manter OCB congelado por 1 semana e comparar gates.
- Perda de evidência → store imutável com SHA-256 e retenção mínima.
- Lock SQLite → executor sem DB; API only; WAL/índices.
- Divergência Q3 → não promover; abrir item com dono e ação.

---

## Sequência obrigatória e regras de execução

1. F1 → F2 → F3 → F4, uma onda por vez.
2. Cada fase só abre a seguinte com o aceite cumprido e registrado.
3. Build + testes verdes antes de qualquer execução no Q4.
4. Q4 é gate; Q3 só depois de green.
5. Falha é fail-fast: parar, classificar, corrigir, reexecutar o mesmo cenário.
6. Nada de UI/espelho antes da fatia vertical provar valor; a exceção é o painel de observabilidade da F2, que é requisito.
7. OCB congelado: nenhuma edição, nem “correção rápida”.

## Fora de escopo (não bloqueia o go-live)

- Anti-repeat semântico, Levenshtein, ONNX ou qualquer heurística nova de similaridade.
- Editor visual completo de circuito.
- Extração operacional do host como processo separado antes da F4; a publicação do pacote independente é entrega da F4.
- Dashboards históricos sofisticados além do painel operacional nativo.
- Novos modelos além do Q4/E2B já usados nos pipes.
- Provas de Q4/Q3 neste host sem GPU.

## Métrica de sucesso do roadmap

- **L1, L2 e L3 verdes no Q4**, com evidência e hashes, executados pelo AI.Manager.
- **Painel nativo** com os 15 quadros, atualização AJAX e zero dependência de Grafana.
- **Zero dependência de runtime do OpenCodeBridge** nos pipes oficiais, com continuidade no componente `Rochas.AI.Manager.Bridge`.
- **Q3 espelho** sem divergência crítica.
- **Um ciclo completo** teste → aceite → treino → avaliação rodando pelo Manager.
