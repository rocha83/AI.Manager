# Bridge de transporte do AI.Manager

Este documento descreve o que vive no pacote `Rochas.AI.Manager.Bridge` e nos
hosts `Rochas.AI.Manager.Bridge.Api` / `Rochas.AI.Manager.Bridge.Runner`. O
objetivo e manter o nucleo de protocolo independente de HTTP server, CLI,
Swagger, banco e Application: ele recebe `JsonObject` + `BridgeOptions` e
devolve `JsonObject`/eventos.

## Camadas

| Camada | Projeto | Responsabilidade |
|---|---|---|
| Contratos | `Rochas.AI.Manager.Bridge.Contracts` | DTOs, schema, canonicalizacao, checksum, `BridgeModelSettings` |
| Nucleo | `Rochas.AI.Manager.Bridge` | traducao Responses/chat, SSE, harness, anti-repeat, breaker, shim |
| Cliente | `Rochas.AI.Manager.Bridge.Client` | HTTP/SSE tipado para um host |
| Host comum | `Rochas.AI.Manager.Bridge.Hosting` | opcoes de CLI, status, contrato |
| REST | `Rochas.AI.Manager.Bridge.Api` | REST + Swagger + `/v1/*` |
| Console | `Rochas.AI.Manager.Bridge.Runner` | `serve` (HttpListener) e `manifest` |

O nucleo nao referencia `Application`, `Domain`, ASP.NET, Swagger nem banco.

## Main harness

`MainHarness.Text` e a base enxuta do caminho gerido. `MainHarness.Apply(messages,
enabled)` substitui o system do cliente e preserva os blocos dinamicos:

* `<env>...</env>`;
* `<available_skills>...</available_skills>`;
* linha `Today's date:`;
* instrucoes pequenas do cliente (em `INSTRUCOES DO CLIENTE`).

O gate e o **cliente**, nunca o sufixo do modelo: hosts chamam
`bridgeOptions.ForClient(User-Agent)` e o core so aplica quando
`MainHarness && !OpencodeClient`. Assim o WAR (`<modelo>-build`) recebe a base
e o CLI do opencode preserva o Code Mode. Nao substitui o master do opencode
(`# Code Mode`, `Instructions from:`, `You are opencode`, system > 6000 chars).

Base e tails **coexistem**: o MainHarness e a base; as tails sao o incremento
do canal. No host, `--main-harness` e o default; no `BridgeOptions` a flag
segue opt-in para consumidores diretos.

## Tails

`HarnessTails.DefaultBehavior` e `HarnessTails.DefaultTools` sao o delta deste
canal (formato, tools primeiro, persistencia, prioridade). O que a base ja cobre
(escrita sem loop, caminhos, 2 tentativas, resposta curta) nao se repete na
cauda. `HarnessTails.Compose(system, harness, behavior, tools)` monta o system
na ordem `system -> behavior -> tools`. O banco (anexo `Behavior`/`ToolsUsage`)
vence o literal. Os aliases `HarnessTails.Behavior`/`Tools` continuam existindo
para o seeder e os testes do produto.

`BridgeOptions.TailScope` decide onde as caudas valem:

* `managed` (default) — todos menos o CLI do opencode (User-Agent);
* `all` — tambem no CLI do opencode;
* `off` — nenhuma.

## Protocolo Responses <-> chat

`ResponsesToChat.Convert` (request) e `ChatToResponses.Convert` (resposta)
aplicam:

* `input` string, array de mensagens, `function_call`, `function_call_output`
  (e variantes `custom_*`, `shell_*`, `computer_*`, `local_shell_*`,
  `apply_patch_*`);
* system consolidado: `instructions` + `system`/`developer` do meio;
* **merge de function_calls consecutivas** em um unico turno do assistente
  (`tool_calls` no mesmo item) — sem isso o template upstream perde a conta de
  “ja chamei isto”;
* `reasoning` do input e ignorado (o upstream chat nao o aceita);
* strip de call narrada no historico quando `LegacyHarness` (`UsesToolCoercion`
  legado): `[Assistant tool call]: nome({...})` nao volta ao modelo;
* tools `namespace` descem um nivel; schema sem `parameters` ganha objeto vazio;
* `pattern` sem ancora `^...$` e removido (parser do llama rejeita o schema
  inteiro com 400);
* tool `execute` (Code Mode) e removida com `ToolStrategy=true`; o bloco
  `# Code Mode` do system e trocado por `ToolSanitizer.CodeModeNote`;
* `max_output_tokens` do cliente vence o teto do motor;
* `SamplingPassthrough` copia `top_p`, `top_k`, `min_p`, `repeat_penalty`,
  `presence_penalty`, `frequency_penalty`, `seed`, `typical_p`, `tfs_z`;
* `QwenStandard` traduz nomes/args OpenAI <-> Alibaba nos dois sentidos
  (`read` <-> `read_file`, `path` <-> `file_path`, etc.).

### Coercao de tool call textual

`ToolCoercion` cobre `<tool_call>`, `<tools>`, `<function>`, fence ` ```json `,
objeto precedido de `<`, objeto cru e `[Assistant tool call]: nome({...})`.
`ToolArgsParser` aceita JSON valido, JSON com escape inventado e pseudo-JS
(chave sem aspas, aspas simples, virgula sobrando). O nome e canonicalizado
(`bash`/`sh`/`exec`/`proc` -> `shell`) e o nome anunciado pelo cliente e
preservado na resposta.

### TextGrammar

`TextGrammar` e a gramatica de texto do protocolo: tags de controle, reparo de
escape (`\;`, `\-`, `\s`), leitura de JSON com chaves balanceadas, strip de call
narrada e `SystemGuardrail` (composicao do system no caminho legado). O
guardrail so entra com `--legacy-harness`.

## Anti-repeat

O controle tem tres escopos:

1. **conteudo** (`ContentAntiRepeat` / `AntiRepeatReply`): cauda verbatim ou
   tres blocos finais similares. A similaridade usa a razao **posicional** de
   caracteres iguais (`AntiRepeat.BlockSimilarity`), como no OCB classico;
   semantica/Levenshtein nao fazem parte do componente.
2. **stream** (`StreamAntiRepeatReply`): mesma regra sobre `thinking` e
   `content`; `ResponsesStreamTranslator.CheckRepeat` marca `RepeatTripped`,
   `RepeatWhere`, `RepeatAt` e `RepeatContext` e o `BridgeProxy` corta o
   upstream.
3. **tool-args** (`ToolArgsAntiRepeatReply`): a partir de 1024 chars de
   argumentos, detecta o mesmo loop e descarta a call parcial; o turno fecha
   com o aviso.

`AntiRepeatSemantic`, `AntiRepeatSemanticForOpencode`, `RepeatSemantic` e
`SemanticDuplicateAt` foram removidos; os testes semânticos saíram junto.

O **breaker** (`ToolsBreaker`) le o historico `assistant.tool_calls` +
`role=tool`:

* falhas consecutivas no fim do historico;
* `BreakerSoft` (default 3): injeta `NudgeNotice` no ultimo resultado;
* `BreakerHard` (default 6): `TripNotice`, **nao chama o modelo** e devolve o
  aviso ao cliente;
* alvos normalizados (sem digitos/separadores): `proj-teste-001` e
  `proj-teste-999` caem na mesma chave;
* se os alvos sao diferentes, nao ha nudge (e progresso, nao loop).

`RepeatedCall`/`AntiRepeat.LoopedToolSignatures` usam assinatura canonica
(`nome(args)` com JSON canonicalizado e apelido de tool), entao
`{"path":"a"}` e `{"path": "a"}` sao a mesma chamada.

Flags: `--breaker-soft`, `--breaker-hard` (0 desliga), `--anti-repeat-window`
no host legado; no componente a janela vem de `BridgeOptions.AntiRepeatWindow`.

## ToolShim (transporte)

`ToolShim` e capacidade de transporte, default desligado (`ToolShimMode=""`).
Com `--tool-shim json`:

* remove `tools`/`tool_choice`/`parallel_tool_calls`;
* injeta o envelope JSON no system;
* forca grammar GBNF e `stream=false` no upstream;
* converte a resposta JSON em `tool_calls` (non-stream, stream sintetizado e
  chat cru).

`ToolShimMinCalls` libera `{"final":...}` depois de N calls; `ToolShimMaxString`
capa strings no grammar.

## ContextGuard

`ContextGuard` roda no non-stream (Responses e chat cru). Quando
`ContextGuard=true` e `ContextTokens>0`, `prompt_tokens/contexto` acima de
`ContextThreshold` (default 0.95) vira `413` com payload
`context_compact_required`. O stream nao e interrompido por usage tardio.

## Hosts

```bash
# REST + Swagger (default 4140)
dotnet run --project src/Rochas.AI.Manager.Bridge.Api -c Release -- \
  --port 4151 --upstream http://127.0.0.1:4100 --model Qwen3.5-9B

# Console (serve ou manifest)
dotnet run --project src/Rochas.AI.Manager.Bridge.Runner -c Release -- \
  serve --port 4152 --upstream http://127.0.0.1:4100 --model Qwen3.5-9B \
  --main-harness --breaker-soft 3 --breaker-hard 6

dotnet run --project src/Rochas.AI.Manager.Bridge.Runner -c Release -- \
  manifest --manifest tests/Rochas.AI.Manager.Bridge.Tests/Fixtures/smoke.manifest.json \
  --workspace /tmp/opencode --model smoke
```

Flags novas: `--main-harness` (default), `--no-main-harness`,
`--tail-scope managed|all|off`, `--legacy-harness`, `--no-legacy-harness`,
`--qwen-standard`, `--no-tool-strategy`, `--sampling-passthrough`,
`--breaker-soft <n>`, `--breaker-hard <n>`.
Ambiente: `BRIDGE_MAIN_HARNESS`, `BRIDGE_TAIL_SCOPE`,
`BRIDGE_LEGACY_HARNESS`, `BRIDGE_QWEN_STANDARD`, `BRIDGE_TOOL_STRATEGY`,
`BRIDGE_BREAKER_SOFT`, `BRIDGE_BREAKER_HARD`, `BRIDGE_SAMPLING_PASSTHROUGH`.

## Gate e espelho

* Q4 `:4124` e o **gate** (`isGate=true`); Q3 `:4125` e **espelho pos-green** e
  nunca gate.
* Nao afrouxar assertion, trocar a instancia do gate nem mexer em
  `max_tokens`/contexto para obter PASS.
* Testes e enunciados sao dados: `ExperimentPlan`, `CircuitStage`,
  `TestScenario`, manifestos em `Fixtures/`.

## Testes de paridade

`tests/Rochas.AI.Manager.Bridge.Tests`:

* `ContentAntiRepeatTests` — conteudo, tool-args, breaker, assinatura canonica;
* `ProtocolFixTests` — merge de calls, strip narrado, execute/pattern,
  coercao pseudo-JS, QwenStandard, MainHarness, tails, sampling;
* `ResponsesStreamParityTests` — args/conteudo em loop no stream;
* `BridgeProxyTests` — breaker trip sem chamar upstream, shim, context guard;
* `FixtureDataTests` — manifesto + upstream Python falso.
