# Plano de aproximação URGENTE — pular o roadmap de testes para o AI.Manager

**Data:** 2026-10-26
**Origem:** sessão 18 (`Sessao 18 - finalizar-revisao-fusao-opencodebridge-com-o-novo-ai-manager.json`) + sessão 17 + `AI-MANAGER-NEXT-STEPS.md` v2 parcial.
**Objetivo:** não esperar as Fases 0–6 do roadmap de testes. Portar os **enunciados (belts)** que hoje são testes hardcoded no OpenCodeBridge para virarem **entidades (`BridgeScenario`) dentro do fluxo de controle de execução (`AI.Manager.Bridge.Flow`)** e rodar no Q4 o mais rápido possível, provando paridade e acelerando a absorção.

**Premissa normativa (sessão 17):** o AI.Manager é o concentrador; o OpenCodeBridge é doador/transitório; `Rochas.AI.Manager.Bridge` é o embrião e **não** deve ser removido; o circuito e os enunciados viram dados do workflow; Q4 é gate; Q3 é espelho pós-green; nenhum PASS por afrouxamento.

---

## 1. O que significa "pular o roadmap de testes"

O roadmap (`AI-MANAGER-NEXT-STEPS.md` §10) prevê Fases 0→6 com UI, workflow, persistência, espelho Q3, etc. Isso é lento e bloqueia a prova de valor. A aproximação urgente **corta o caminho**:

- **Não** implementar UI de catálogo/editor/console antes de provar execução.
- **Não** esperar `ExperimentPlan`/`TestScenario` no banco para rodar o primeiro belt.
- **Sim** usar o contrato que **já existe** (`BridgeManifest`, `BridgeScenario`, `BridgeManifestValidator`, `ManifestCommandResolver`, `Bridge.Runner manifest`) + o host (`serve`) + Q4, e portar apenas o **kernel de execução de cenário** (`TestCircuitDriver`/`ManifestCircuitTemplate`) para `AI.Manager.Bridge.Flow`.
- **Sim** tomar os enunciados das belts (`azul..preta`) do `E2EBeltsBuildTests`/`E2EBeltsTests` e transformá-los em `BridgeScenario` num manifesto `war-baseline.json`, rodando via `Bridge.Runner` contra Q4.

Resultado esperado: em dias (não semanas), o AI.Manager.Bridge executa os belts no Q4 e produz o mesmo gate (`PASS/FAIL/STALE`) do host legado, sem UI e sem banco novo.

## 2. Estado real (o que já existe e pode ser reusado)

| Componente | Onde | Reuso |
|---|---|---|
| Protocolo/transporte | `AI.Manager.Bridge` (Responses/Chat/SSE, anti-repeat, harness, breaker, coerção) | Já portado; não mexer |
| Contratos de manifesto | `Bridge.Contracts` (`BridgeManifest`, `BridgeScenario`, assertions, validator, canonicalizer) | Já definidos; só popular |
| Dry-run/resolução | `Bridge.Flow.ManifestCommandResolver` | Já resolve comandos |
| Host HTTP | `Bridge.Runner serve` (`/api/status`, `/v1/*`, `/api/manifest/validate|dry-run`) | Já serve Q4/Q3 |
| Cliente | `Bridge.Client` | Reuso futuro |
| Kernel de circuito | `OpenCodeBridge.Test/TestCircuitDriver.cs` | **A portar** para `AI.Manager.Bridge.Flow` |
| Enunciados (belts) | `E2EBeltsBuildTests.Belts`, `E2EBeltsTests.Belts` | **A extrair** para seed JSON |
| WAR/template | `WarTemplate.cs` | Oráculo de paridade (não runtime) |

**Lacuna única bloqueante:** não existe executor de cenário (`native`) no `AI.Manager.Bridge` que chame `/v1/chat/completions` com tool `write` em loop de turnos e aplique assertions. Isso é o que a fatia vertical entrega.

## 3. Escopo da fatia vertical (o que entra / o que não entra)

**Entra (mínimo para rodar 1 belt no Q4):**
1. Portar `TestCircuitDriver` (kernel) para `AI.Manager.Bridge.Flow` (namespace interno, sem dependência de `Application`/`Domain`).
2. Criar `NativeScenarioExecutor` em `Flow/` que, dado um `BridgeScenario`, monta histórico (system+user), chama `BridgeUrl/v1/chat/completions` com `tool_choice=required` e tool `write`, executa turnos até `filesCreated>=3` ou stall, aplica assertions (`fileCountAtLeast`, `totalCharsAtLeast`, `fileExtensionCountAtLeast`), devolve `Passed/Failed`.
3. `ManifestCircuitTemplate` mapeando `BridgeStage`/`BridgeStep`/`BridgeScenario` → execução (step kind `native` chama o executor de cenário).
4. Gerar `AI.Manager/Data/Seeds/war-baseline.json` com L1 build (azul+verde) como cenários, com `sourceCommit` + `manifestHash`.
5. CLI `Bridge.Runner manifest --manifest X --result Y` já valida+resolve; adicionar flag `--execute` (ou novo comando `run`) que executa o manifesto e grava resultado JSON (gate, steps, scenarios, métricas, evidência).
6. Rodar no Q4 `:4124` e comparar com host legado.

**Não entra (fica para depois):**
- UI (catálogo, editor, console, espelho).
- Persistência em SQLite (`ExperimentPlan`, `TestScenario`, `TestRun`) — o manifesto JSON é a fonte nesta fatia.
- Q3 espelho (só depois do Q4 verde).
- L2/L3 e marrom/preta (depois da roxa verde).
- Remoção do host legado (só após 3 gates Q4 verdes).

## 4. Mapeamento belt → cenário (enunciados como entidades)

Os enunciados passam a ser `BridgeScenario.Prompt` + `SystemPrompt`, com assertions derivadas do código dos belts.

| Belt | Source | Scenario key | Assertions mínimas | Loop |
|---|---|---|---|---|
| azul | `E2EBeltsBuildTests.Belts[azul]` | `azul-build` | `fileCountAtLeast 3`, `totalCharsAtLeast 2000`, `fileExtensionCountAtLeast .cs 2` | maxTurns 10, nudges 2, review true |
| verde | idem | `verde-build` | idem + `.sql 1` | idem |
| roxa | idem | `roxa-build` | `fileCountAtLeast 3`, `totalCharsAtLeast 2000`, `.cs 2` | idem (já verde no Q4: 8 arquivos, 16.327 chars) |
| marrom | idem | `marrom-build` | idem + `.tsx 1` | idem |
| preta | idem | `preta-build` | idem | idem |

Orch (multi-role) fica como cenário separado (`*-orch`) com `ScenarioPipeline` (fase posterior; na fatia vertical, só build belts).

SystemPrompt padrão (do belt build direct): `"Voce e arquiteto desenvolvedor senior (role build), dialogo em pt-br. Trabalhe em {ws}. Implemente os artefatos pedidos direto no disco, sem plano e sem prosa. Crie pelo menos 3 arquivos .cs/.sql/.tsx usando caminhos absolutos dentro de {ws}. Ao terminar responda apenas BUILD-OK com o resumo dos arquivos."`

Tools: `{ "mode": "required", "allow": ["write"] }` (e `read` na fase de review).

## 5. Etapas de implementação (ordem, com aceite)

### E1 — Portar o kernel de fluxo (1–2 dias)
- Criar `AI.Manager/src/Rochas.AI.Manager.Bridge/Flow/TestCircuitDriver.cs` portado do OCB (lock, pausa, DAG, retry, artefatos, gate). Remover dependências de `Rochas.OpenCodeBridge.Test`; usar `Bridge.Contracts` para tipos.
- Criar `AI.Manager/src/Rochas.AI.Manager.Bridge/Flow/ManifestCircuitTemplate.cs` (mapeamento manifesto → `CircuitStepSpec`).
- Criar `AI.Manager/src/Rochas.AI.Manager.Bridge/Flow/CircuitStepSpec.cs`, `GatePolicy.cs` (se não existirem).
- **Aceite:** `dotnet build Rochas.AI.Manager.slnx -c Release` 0/0; teste unitário mínimo (`TestCircuitDriverTests`) passando.

### E2 — Executor de cenário nativo (1 dia)
- Criar `AI.Manager/src/Rochas.AI.Manager.Bridge/Flow/NativeScenarioExecutor.cs` implementando o loop do belt (turnos, nudges, stall, review, assertions).
- Usar `HttpClient` para `/v1/chat/completions` no `BridgeUrl`; tool `write` (e `read` na review).
- Aplicar assertions de `BridgeAssertion`.
- **Aceite:** rodar 1 cenário (azul) isolado via teste xUnit contra Q4 e obter PASS.

### E3 — Comando de execução no runner (0,5 dia)
- Adicionar `run` ao `Bridge.Runner` (ou `manifest --execute`): valida+resolve+executa, grava `--result` com `BridgeResult` (status, gate, stages, scenarios, métricas, evidência).
- Reusar `BridgeResult` existente.
- **Aceite:** `dotnet run --project ... -- run --manifest X --result Y` gera JSON com gate verde para azul.

### E4 — Seed `war-baseline.json` (0,5 dia)
- Criar `AI.Manager/Data/Seeds/war-baseline.json` com L1 build (azul+verde) como `BridgeManifest` válido, `schemaVersion 1.0`, `sourceCommit` = HEAD do OCB, `manifestHash` calculado.
- Validar com `BridgeManifestValidator` (teste).
- **Aceite:** `manifest validate` retorna válido; hash confere.

### E5 — Primeira execução Q4 (0,5 dia)
- Subir Q4 `:4124` (já operacional) + llama `:4100`.
- Rodar `Bridge.Runner run --manifest war-baseline.json --result result-azul.json` apontando para Q4.
- Comparar com host legado (mesmo comando `--only E-belts-build-direct --belts azul,verde`).
- **Aceite:** gate verde no Q4; métricas (arquivos, chars) dentro de ±20% do legado; nenhum PASS por afrouxamento.

### E6 — Repetir para roxa/marrom/preta (1–2 dias)
- Adicionar cenários roxa (já verde no legado), marrom, preta ao manifesto.
- Rodar L2 (roxa) e L3 (marrom+preta) no Q4.
- **Aceite:** 3 gates Q4 verdes consecutivos por nível antes de cortar o host legado.

### E7 — Paridade e cutover (depois)
- Shadow runs; flag `executor=bridge` vs `executor=legacy`.
- Só arquivar OCB após paridade + 3 gates verdes consecutivos por nível.

## 6. Arquivos a criar/editar (lista fechada)

```
AI.Manager/src/Rochas.AI.Manager.Bridge/Flow/TestCircuitDriver.cs          (novo)
AI.Manager/src/Rochas.AI.Manager.Bridge/Flow/ManifestCircuitTemplate.cs     (novo)
AI.Manager/src/Rochas.AI.Manager.Bridge/Flow/NativeScenarioExecutor.cs      (novo)
AI.Manager/src/Rochas.AI.Manager.Bridge/Flow/CircuitStepSpec.cs             (novo, se ausente)
AI.Manager/src/Rochas.AI.Manager.Bridge/Flow/GatePolicy.cs                 (novo, se ausente)
AI.Manager/src/Rochas.AI.Manager.Bridge.Runner/Program.cs                  (editar: comando run)
AI.Manager/Data/Seeds/war-baseline.json                                    (novo)
AI.Manager/tests/Rochas.AI.Manager.Bridge.Tests/NativeScenarioExecutorTests.cs (novo)
```

**Regra de dependência:** `AI.Manager.Bridge` não referencia `Application`/`Domain` (já garantido). `NativeScenarioExecutor` usa apenas `HttpClient` + `Bridge.Contracts` + `BridgeOptions`.

## 7. Riscos e mitigação (urgente)

| Risco | Mitigação |
|---|---|
| Portar `TestCircuitDriver` inteiro é grande (1132 linhas) | Portar só o necessário para cenário `native` (loop de turnos + assertions); DAG/retry/lock ficam para a fatia de circuito completo. Reusar o do OCB via referência de projeto durante a transição, se necessário. |
| Q4 ocupado/instável | Rodar em janela dedicada; não reiniciar Q4 sem autorização; conferir `/api/harness` (chars) antes de declarar PASS. |
| Divergência de métricas (chars/arquivos) | Tolerância ±20% na primeira rodada; ajustar prompts/assertions depois, nunca afrouxar gate. |
| Enunciado "Especifique" induz prosa | Usar versão revisada (imperativo, "va direto ao write") já validada na roxa. |
| `write` repetido / anti-repeat | Reusar `AntiRepeat`/`ToolsAntiRepeat` já portados no Bridge. |
| Big-bang do `Program.cs` | Não portar `Program.cs` do OCB; só o runner `manifest`/`run`. |

## 8. Critérios de aceite da fatia vertical

1. `dotnet build Rochas.AI.Manager.slnx -c Release` = 0 erros/0 warnings.
2. `Bridge.Tests` verdes (existentes + novos).
3. `war-baseline.json` valida (`BridgeManifestValidator`) e hash confere.
4. `Bridge.Runner run` executa azul no Q4 e retorna gate verde.
5. Métricas do AI.Manager.Bridge dentro de ±20% do host legado para o mesmo belt.
6. Nenhum PASS obtido por afrouxar assertiva, trocar instância ou mudar `max_tokens`/ctx.
7. Q3 não é usado como gate (só depois do Q4 verde).

## 9. Próximo passo imediato (hoje)

1. Criar `NativeScenarioExecutor.cs` (E2) — é o menor incremento que prova valor.
2. Testá-lo contra Q4 com um cenário azul hardcoded em xUnit.
3. Se verde, gerar `war-baseline.json` (E4) e rodar via runner (E3+E5).
4. Reportar métricas e decidir se expande para roxa/marrom/preta.

**Não** começar pela UI, pelo banco ou pelo espelho Q3. A prova de valor é: belt rodando no Q4 via `AI.Manager.Bridge`.
