## feat(anti-repeat): sonda semantica no opencode cobre content e thinking

- Date: 2026-10-26

- Fechada a lacuna apontada na sessao 18: o anti-repeat do Bridge era so
  verbatim/similaridade posicional e nao via a mesma pergunta/recomendacao
  reformulada. `AntiRepeat.SemanticDuplicateAt` compara a cauda com janelas
  anteriores por tokens de conteudo (12 tokens, acentos normalizados,
  stopwords pt/en), Dice >= 0.72 e pelo menos 8 tokens compartilhados, com
  lookback de ate `6 * window` caracteres.
- A sonda roda no `ResponsesStreamTranslator.CheckRepeat` para `content` e
  `thinking`, com throttle de 64 caracteres e piso de `2 * window`. O caminho
  de aviso/retry (`RepeatTripped`, `RepeatWhere`, `RepeatAt`,
  `RepeatContext`) continua o mesmo; `RepeatSemantic` apenas identifica a
  origem. `Reset` zera o throttle.
- Escopo: a sonda fica **desligada por padrao**; `AntiRepeatSemantic =
  true` liga para todos e `AntiRepeatSemanticForOpencode = true` liga via
  `BridgeOptions.ForClient` somente no User-Agent opencode. Outros clientes
  mantem a paridade com o legado. Nao entra ONNX/embeddings nesta urgencia; a
  sonda lexical e conservadora e fica isolada para medicao no host com GPU.
- **Prova**: SDK .NET `10.0.403`; `Bridge.Tests` 110 PASS e
  `AI.Manager.Tests` 97 PASS + 1 SKIP; 0 falhas. Testes novos cobrem parafrase no detector, ativacao por cliente,
  content, thinking, default desligado e prosa normal sem falso-positivo.

## docs(c4): C4 e arquitetura v2.1 refletem o porte do AI.Manager.Bridge

- Date: 2026-10-25

- C4 (Nível 2, contêineres) regenerado a partir de `gen-c4.py`: o boundary
  `AI.Manager.Bridge` agora mostra os seis projetos reais (`Core`, `Contracts`,
  `Client`, `Hosting`, `Api`, `Runner`), o pipeline protocol-only, `MainHarness`
  base + `HarnessTails`, `TailScope` e o discriminador por cliente (User-Agent).
  `TestBench` e `Seed do WAR` ficam marcados como fase 2; Q4 segue gate e Q3
  espelho pós-green. Saídas: `.drawio` editável e PNG 4002x2402 (escala 2.5).
- `Rochas.AI.Manager-Arquitetura v2.md` sobe para v2.1: sumário, estrutura de
  projetos, regra de dependência do Bridge, seção 9.5 (harness base+tails),
  seção 10 reescrita (legado transitório, Bridge, cutover e invariantes),
  testes, ADR-13/ADR-14, riscos, roadmap (Fase 3.5), critérios, mapa, glossário
  e change log. `ARQUITETURA-CAMADAS.md` ganhou a camada Bridge e hosts.
- **Prova**: `python3 docs/ai-manager/diagrams/gen-c4.py` + `drawio -x -f png
  --size page -s 2.5`; XML válido; build/testes verdes no commit `c72f726`
  (`Bridge.Tests` 102 PASS; `AI.Manager.Tests` 97 PASS + 1 SKIP).

## sync(harness): base enxuta do OCB, tails coexistentes e discriminador por cliente

- Date: 2026-10-25

- Puxado o estado atual do `OpenCodeBridge` (`2315dab` + `5be94c4`):
  `MainHarness` vira base enxuta (144 linhas, sem hardcode de infra) e
  `Apply(messages, enabled)` deixa de receber `hasProfile`; o gate passa a ser o
  **cliente** (`User-Agent` contem "opencode"), nunca o sufixo do modelo — o WAR
  usa `<modelo>-build` e agora recebe a base. O CLI do opencode preserva o
  Code Mode.
- `HarnessTails.DefaultBehavior` fica so com o delta do canal (formato, tools
  primeiro, persistencia, prioridade); escrita sem loop, caminhos e "2
  tentativas" migraram para a base. `Compose` e os aliases
  `Behavior`/`Tools` foram preservados para o seeder e os testes do produto.
- Base e tails **coexistem** (removida a dispensa `!mainHarness`).
  `BridgeOptions.TailScope` = `managed`|`all`|`off` (default `managed`: todos
  menos o CLI do opencode). `BridgeOptions.ForClient(User-Agent)` isola a
  decisao por requisicao — o host e singleton e nao muta a instancia
  compartilhada. Flags: `--tail-scope`/`BRIDGE_TAIL_SCOPE`; `--main-harness`
  passa a ser default no host.
- **Prova**: `dotnet build Rochas.AI.Manager.slnx -c Release` = 0 erros /
  106 warnings; `Bridge.Tests` = 102 PASS; `AI.Manager.Tests` = 97 PASS +
  1 SKIP. Nao portado por ser fora do transporte: `Tools/*` (executor do Core),
  `ChatterGuard` (stateful por sessao), `RoleProfiles`/`PermissionPolicy`
  (host/role).

## feat(ai-manager-bridge): nucleo de protocolo independente + paridade OpenCodeBridge (main harness, tails, anti-repeat e fixes)

- Date: 2026-10-25

- **Strangler concluido (fatia de inteligencia)**: `Rochas.AI.Manager.Bridge`
  deixa de referenciar Application/Domain e passa a depender so de
  `Bridge.Contracts`; ASP.NET, CLI, Swagger, runner e cliente HTTP saem para
  projetos proprios: `Bridge.Contracts` (DTOs/schema/canonicalizacao/checksum),
  `Bridge.Client` (HTTP/SSE), `Bridge.Hosting` (flags/status),
  `Bridge.Api` (REST+Swagger) e `Bridge.Runner` (console `serve`/`manifest`).
  `Rochas.AI.Manager.slnx` e o `.gitignore` (Python) acompanham.
- **MainHarness**: portado do OCB com substituicao do system no caminho gerido
  (`hasProfile=false`), preservando `<env>`, `<available_skills>`,
  `Today's date:` e instrucoes pequenas do cliente; nao troca o master do
  opencode (`# Code Mode`, `Instructions from:`, system > 6k chars). Quando
  aplica, as caudas de harness sao dispensadas (nucleo corretivo em um lugar so).
- **Tails**: `HarnessTails.DefaultBehavior`/`DefaultTools`
  (workspace/formato/escrita anti-loop/tools primeiro/persistencia) com
  `Compose(system, harness, behavior, tools)`; os aliases `Behavior`/`Tools`
  mantem o `BridgeFeatureSeeder` e os testes do produto.
- **Fixes de protocolo** em `ResponsesToChat`/`ChatToResponses`/`ResponsesStream`:
  merge de `function_call` consecutivas no MESMO turno do assistente; strip da
  call narrada no historico com `LegacyHarness`; apelido canonico
  `bash/sh/exec/proc -> shell` preservando o nome anunciado pelo cliente;
  `execute`/Code Mode removidos com `ToolStrategy`; `pattern` nao ancorado
  removido; `max_output_tokens` do cliente vence o teto; `SamplingPassthrough`
  (`top_p`, `top_k`, `min_p`, `repeat_penalty`, `presence_penalty`,
  `frequency_penalty`, `seed`, `typical_p`, `tfs_z`); traducao `QwenStandard`
  OpenAI<->Alibaba nos dois sentidos (tools, `required` e argumentos).
- **Anti-repeat (3 escopos)**: conteudo (`ContentAntiRepeat` com similaridade
  Levenshtein, nao comparacao posicional), stream (`StreamAntiRepeatReply` com
  `RepeatTripped`/`RepeatWhere`/`RepeatAt`) e tool-args
  (`ToolArgsAntiRepeatReply` a partir de 1024 chars; descarta a call parcial e
  fecha o turno com aviso). `ToolsBreaker` le `assistant.tool_calls` +
  `role=tool`: `BreakerSoft=3` injeta nudge no ultimo resultado,
  `BreakerHard=6` faz trip SEM chamar o upstream; alvos normalizados
  (digitos/separadores ignorados) e assinatura canonica (`nome(args)` com JSON
  canonicalizado) provam que `{"path":"a"}` e `{"path": "a"}` sao a mesma call.
- **Coercao/gramatica**: `ToolCoercion` (templates `<tool_call>`, `<tools>`,
  `<function>`, fence json, objeto cru e `[Assistant tool call]`) +
  `ToolArgsParser` (pseudo-JS: chave sem aspas, aspas simples, virgula sobrando)
  + `TextGrammar` (tags de controle, escapes, JSON balanceado, guardrail
  legado). `ToolSanitizer` centraliza `execute`/Code Mode e pattern-strip.
- **Hosts**: `Bridge.Api` expoe `/api/status`, `/health`, `/api/contract`,
  `/api/contract/schema`, `/api/manifest/validate|dry-run`, `/v1/responses`,
  `/v1/chat/completions` e Swagger; `Bridge.Runner` cobre `serve` e
  `manifest validate|dry-run`. Flags novas: `--main-harness`, `--legacy-harness`,
  `--qwen-standard`, `--no-tool-strategy`, `--sampling-passthrough`,
  `--breaker-soft/--breaker-hard` (env `BRIDGE_*` equivalentes).
- **Fixture de transporte**: `fake_llama.py` + `smoke.manifest.json` + README em
  `tests/Rochas.AI.Manager.Bridge.Tests/Fixtures`, copiados no build; o E2E
  Python so roda se `python3` existir (ausencia nao falha o transporte .NET).
- **Docs**: `docs/ai-manager/BRIDGE-TRANSPORT.md` com camadas, harness, fixes,
  anti-repeat, shim, gate Q4/Q3, flags e testes. `DEV-HISTORY.md` renomeado
  para `HISTORY.md` (conteudo historico preservado).
- **Prova**: `dotnet build Rochas.AI.Manager.slnx -c Release` = 0 erros /
  112 warnings; `Bridge.Tests` = 98 PASS; `AI.Manager.Tests` = 97 PASS +
  1 SKIP. O gate segue Q4 `:4124`; Q3 `:4125` e espelho pos-green, nunca gate.

## fix(bridge-engine): regra de escrita contra loop de `write` e aviso de contencao acionavel

- Date: 2026-10-20

- Prova do loop (log da bridge, GPU): o mesmo `write` (mesmos argumentos) apareceu
  **2x** no historico do chat (msgs 117/120 e 125/128) e a 3a tentativa caiu em
  `anti-repeat-loop` com `tool=write` — o turno fechava sem tool call e sem texto
  util, dando a impressao de travamento.
- **Enunciado (dado)**: `Data/harness-behavior.md` passa a ser o texto canonico
  do anexo `Behavior` (lido pelo seeder), com a regra
  `COMPORTAMENTO (escrita)`: `write` uma vez por arquivo/conteudo, correcao e
  `edit` no trecho exato, nunca reemitir a mesma chamada (path+conteudo) sem
  confirmar com `read`/`grep`, erro de tool pede argumento novo (max 2 tentativas).
  `HarnessTails.Behavior` fica como fallback do motor.
- **Aviso de contencao (logica)**: `AntiRepeat.DefaultNotice` agora diz o que
  fazer (confirmar com `read`/`grep`, corrigir com `edit`), em vez de apenas mandar
  encerrar em silencio.
- Corrigido fragmento concatenado por engano no `HarnessTails.Tools`
  (`"digitando" + "nao existe"` iam para o system prompt).
- Testes: 97 aprovados, 1 ignorado (`HarnessTailsTests`: regra de escrita presente,
  workspace/formato preservados, tools sem fragmento, aviso acionavel).

## feat(bridge-engine): motor de protocolo no AI.Manager (tradução, stream, anti-repeat) e flag de coerção no cadastro do modelo

- Date: 2026-10-20

- Novo projeto `Rochas.AI.Manager.Bridge` (sem ASP.NET): `ResponsesToChat`
  (input/tools/caudas), `ChatToResponses` (reasoning nativo + message +
  function_call), `ResponsesStreamTranslator` (SSE com `reasoning_text.delta`,
  `streamPerDelta`, hold-back de tag partida), `ResponsesSse` (síntese a partir
  do JSON), `AntiRepeat` (janela/retry/assinatura de tool), `ControlTags`,
  `ToolCallTextExtractor` (tag/plural/fence/ângulo/narrado) e `HarnessTails`.
- **Enunciado é dado**: `HarnessAttachment` ganhou as peças `Behavior`
  (workspace/formato) e `ToolsUsage` (como chamar tool), semeadas pelo
  `BridgeFeatureSeeder` e lidas por `HarnessContentProvider`;
  `BridgeOptionsFactory.FromContent` só cai no default quando não há dado.
  O behavior saiu do `harness-system.md` (Core) para não duplicar no prompt.
- **Anti-repeat é lógica, não dado**: janela, retry, detecção e aviso ficam no
  motor (`BridgeOptions`), sem anexo semeado.
- **Coerção por modelo**: `ModelBinding.UsesToolCoercion`
  (coluna `uses_tool_coercion`, migration no `AppDb`), exposto na API
  (`ModelBindingsCreate/UpdateRequest`) e no cadastro Web ("Utiliza coerção de
  tools"); `BridgeOptionsFactory.FromModel` mapeia o flag para `CoerceNarrated`.
- Testes: 93 aprovados, 1 ignorado — tradução request/response, tools/pattern,
  stream (ordem, hold-back, granularidade, function_call), anti-repeat,
  factory (dado vence default; coerção vem do cadastro).

## unify(binding-bridge): um único caminho de encaminhamento e endpoint resolvido pelo agente

- Date: 2026-10-20

- `BindingBridgeController`: as rotas `chat/completions` e `responses` duplicavam
  todo o encaminhamento (auth, binding, stream, run). Agora as duas delegam a
  `ForwardAsync(path, body, sanitizeChat)`; a resolução do endpoint saiu do
  hardcode `localhost:4124/4125` para `Agent.BridgeUrl` → `ModelBinding.Provider`
  → `Bridge:GpuUrl`/`Bridge:CpuUrl` (`appsettings`), com `Bridge:ApiKey`.
- `ReasoningSanitizer.SanitizeResponses`: tira tags de controle
  (`<|channel>`, `<thought>`, ...) do `content[].text`/`summary[].text` do item
  `reasoning`, preservando o shape nativo (`reasoning_text` + `item.content`);
  aplicado na rota `responses` não-stream (antes não havia nenhuma limpeza).
- Seeder (enunciados): `task` → `subagent` em `Agent.Tools`, `ToolPolicy` e nos
  prompts `harness-system.md`/`harness-nucleo.md`; `subagent` liberado em
  plan/build/orch/moe/scripter e negado em exec; behavior tail
  (workspace/formato) incorporado ao `harness-system.md`.
- O seeder agora é **idempotente e sincronizante**: `ToolListNormalizer`
  (Application/Helpers) troca `task` → `subagent`, adiciona nas roles que
  delegam e remove no Exec; attachments `Core`/`Tools`/`Nucleo` são atualizados
  quando o recurso embutido muda (antes o conteúdo antigo ficava preso no banco
  porque o seed só inseria quando não existia).
- Testes: 74 aprovados, 1 ignorado (pré-existente); `ReasoningControlTagTests`
  cobre strip de tags/preservação da mensagem e `ToolListNormalizerTests` cobre
  a migração de token (legado, adição, remoção no Exec, seed canônico).

## 9ccd377 - feat(ai-manager): vertical slice harness profiles (in-memory service + linter + MVC/AJAX UI)

- Commit: 9ccd3776b11adb05f3e1b9309adbe6406a524a9d
- Date: 2026-10-19

## 1a24746 - feat(ai-manager): backend completo DDD + DapperRepository + BaseEntity + API CRUD (int PK)

- Commit: 1a247464b5e339dfe0bad86779cf1798593a7a0f
- Date: 2026-10-19

## 2f21239 - feat(ai-manager): frontend base (layout sidebar responsivo, modals, CRUDs users/agents/modelartifacts/trainingjobs)

- Commit: 2f2123959248d76cb1df74b31a2e4852d45a8a2e
- Date: 2026-10-19

## a5e4643 - feat(ai-manager): split em projetos distintos Web (MVC) e Api (Swagger), compartilhando Application/Domain

- Commit: a5e4643c6d98c1f104305e3a931a2e65ec56d069
- Date: 2026-10-19

## 479c729 - docs(ai-manager): guia do iniciante (harness, treinamento, uso)

- Commit: 479c729e8e2212aeb114580bb3ba2103588ceaa4
- Date: 2026-10-19

## 66ba98e - docs(ai-manager): guia de tuning para equipe tecnica

- Commit: 66ba98e90e4908472eac65c52c6ca38b29858e88
- Date: 2026-10-19

## ba70544 - docs(ai-manager): inclui testes E2E de belts no guia tecnico

- Commit: ba705444c6879493bb1eb0822c7da5ab9dc08595
- Date: 2026-10-19

## e5fa01b - feat(ai-manager): UX padrao (required, busca em grids, cards mobile)

- Commit: e5fa01bdd71704a7ba327813502a1fdfbd8a32ef
- Date: 2026-10-19

## 2935c70 - feat(ai-manager): training engine distinto com monitoramento e ativacao de harness

- Commit: 2935c7072758a5e41d6f02b1277be5132cb2c7f9
- Date: 2026-10-19

## 9268fcc - refactor(ai-manager): interfaces centralizadas no Domain + doc de camadas

- Commit: 9268fccb2b1bc3cf45db7d0f7c3149c88392c4ae
- Date: 2026-10-19

## ab5a3c2 - fix(harness): remove delegacao do core no seeder (vai para tail orch)

- Commit: ab5a3c22705b4d2b02413e69649e3f6b5d7adc27
- Date: 2026-10-19

## 94605cf - feat(harness): tail orch usa subagente do mesmo tipo para contextos <=64k

- Commit: 94605cf2483e4cf3bb28016006f7de4af5a11a59
- Date: 2026-10-19

## 0f6e11e - fix(harness): subagente <=64k no core global (nao apenas orch)

- Commit: 0f6e11ee8e747f5b066b373dc199fa3975f5e82b
- Date: 2026-10-19

## 44341ff - docs/fix(harness): subagente mesmo tipo/nome de modelo no core + historico

- Commit: 44341ffb6bb1a6f116a553ba37d65879107ec751
- Date: 2026-10-19

## 7b478dd - fix(harness): ajuste texto subagente (contextos <=64k)

- Commit: 7b478dd53e99565e847c3021a020c9b356dfb0f7
- Date: 2026-10-19

## 42d1c4e - feat(ai-manager): login, seed usuarios e AppDb com colunas base (name/is_active/code)

- Commit: 42d1c4ea7572e096dc5c9e83761d0b4cb113be0c
- Date: 2026-10-19

## 76600a1 - feat(ui): upload estilizado e filtros de data em grids

- Commit: 76600a1a7d5d5ebc465a55e739a7cd6abfe83c78
- Date: 2026-10-19

## 25ccee8 - fix(ai-manager): init do banco (colunas duplicadas), DI da API e partial de preview

- Commit: 25ccee83a5d7500d3e3691a706d3654e144c449d
- Date: 2026-10-19

## 191efd4 - feat(ai-manager): fluxo de treinamento com avaliador de acuracia, workflow generico (sgev3), serilog, /health, fix persistencia Update

- Commit: 191efd437d0915722a2286837c6b02702d0d734a
- Date: 2026-10-19

## 7ab5288 - feat(ai-manager): seeder de EntityStatus/WorkflowStep por entidade (Gemma Forge) + hashcode djb2 consistente com sgev3

- Commit: 7ab5288111a8b9d64ab34a8c5a63d3746fb1b20d
- Date: 2026-10-19

## b4890df - feat(ai-manager): status do fluxo em pt-BR (nomes) com codigos en-US + seeder

- Commit: b4890df1273c2b4b781e942ca74b27facca5a04b
- Date: 2026-10-19

## 0c29a52 - feat: TorchSharp training engine + reports (PDF/Excel/CSV) + IReportService

- Commit: 0c29a5213861f9ab36e9dfb96a450c53392b43af
- Date: 2026-10-19

## 9c31a52 - feat: BridgeFeatureSeeder (models/agents/bindings/harness/policies) + AppDb created_at fixes

- Commit: 9c31a5244e05fca802e1e9959517b532635d2d5f
- Date: 2026-10-19

## e071a1b - feat: HarnessScenarioSeeder (devpipe/belt/scientific/mission-tools/general templates)

- Commit: e071a1bcbc1533746044ce92f0b0bed2feed4343
- Date: 2026-10-19

## 01e3c83 - feat: harness completo opencode (system.md 14k + tools.json 15k) como attachments embedded

- Commit: 01e3c834f24da11d6375e8d6bf987cbe9279e095
- Date: 2026-10-19

## 37958b4 - feat: API /api/harness/{id}/content (bridge consome harness completo)

- Commit: 37958b4cb56053dfbffe53d384b437871896a6b4
- Date: 2026-10-19

## da089f8 - feat: nucleo universal expandido (disciplina tools) como attachment Kind=Nucleo

- Commit: da089f8227f44b36c69da3279d6a7d7a6ed61f66
- Date: 2026-10-19

## 55d0690 - feat: proxy OpenAI-compatible /v1/chat/completions (AI.Manager como bridge)

- Commit: 55d06904e03a6cd10879ff6833c3dd389c8f3fee
- Date: 2026-10-19

## 477aa52 - feat: proxy intelligence (coerce/sanitize/SSE/context-guard/metrics/auth)

- Commit: 477aa522f29f8363c1adddb4674630486983bdfd
- Date: 2026-10-19

## 17cbdca - feat: proxy profiles por role (temperature/thinking) + cenário build-puro

- Commit: 17cbdca612b2e5ec3edbdb967052c142c06db3e9
- Date: 2026-10-19

## 13ef89c - feat: Chat view (MDB) portada da bridge + ChatController

- Commit: 13ef89c9ea7e91f151b8b531e574e6d15f23907e
- Date: 2026-10-19

## 3ae624c - feat: autenticação JWT + autorização + anti-forgery em toda plataforma

- Commit: 3ae624c3d23fb7a2965fec0cc8794d895caadfea
- Date: 2026-10-19

## b5122b4 - feat: ScenarioTemplateSeeder (topics/devpipe/belts/scientific/mission-tools)

- Commit: b5122b4a35b4dc45500b96f2e730007e45806c2a
- Date: 2026-10-19

## fcd6b62 - test: cobertura ~80% (59 testes, unit+integracao)

- Commit: fcd6b62542d53e73f634dddd772bd5ea481ccb1b
- Date: 2026-10-19

## a0a9f0a - feat: painel de aprovações pendentes (admin) + role Admin no login

- Commit: a0a9f0a9f1b3e1ebe3ff83eec241cc30351d5918
- Date: 2026-10-20

## 72e5836 - feat: RejectAsync com WorkFlowTransition + badge tooltip motivo (view)

- Commit: 72e5836d79b9e530d9589a5f0daf04e9d1db970c
- Date: 2026-10-20

## c52ce86 - refactor: nomenclatura legível (sem siglas/1-char) em novos arquivos

- Commit: c52ce86470554f2103ed5dc2bc95eb74ec633c0b
- Date: 2026-10-20

## bcf2b9d - feat: MoveAsync registra WorkFlowTransition antes de estampar status

- Commit: bcf2b9d8c5cf8892221cc0dd835b59343ec36a6a
- Date: 2026-10-20

## 8a6fe6e - refactor: nomenclatura legível em testes (r->result, h->hash)

- Commit: 8a6fe6e30dbd106a8975e4b6dbc568a52b2b297d
- Date: 2026-10-20

## d89fe85 - test: TrainingWorkflowService Evaluate/UpdateAccuracy (61 testes verdes)

- Commit: d89fe85d2aeb157b1888a76842417cca265408fb
- Date: 2026-10-20

## 0ad39a1 - feat: motor TorchSharp puro C# (sem libtorch/pytorch), QLoRA runner externo, seeders SFT/DPO/RM, porte tools bridge, refatoracao ORM (sem QueryRaw/ADO.NET), testes 62 verdes

- Commit: 0ad39a16d3a6ae459f77f86d6ec48b601a32f718
- Date: 2026-10-20

## fd2e861 - feat: dataset upload + download templates nativos (SFT/DPO/RM) na web UI

- Commit: fd2e86111695bbebbc6532fbf8863b4c9956675f
- Date: 2026-10-20

## 795193f - docs+relatorios: v1 as-built, v2, REPORT-PENDINGS e filtros De/Ate

- Commit: 795193ff472f7f21df34e7e4eca7376bec52a58a
- Date: 2026-10-20

## ba8c257 - chore: alteracoes pendentes de datasets (upload/validacao/templates)

- Commit: ba8c257377ef839b09a0d17dd1210ff9579a58a7
- Date: 2026-10-20

## 41fb198 - feat: relatorios Fase 1 - ReportDefinition/catalog, endpoints, SQL pagination (TrainingJob), half-open De/Ate

- Commit: 41fb198a1a6e1b912336f4d6651a080fdb66ffdc
- Date: 2026-10-20

## 6dbc447 - feat: JWT secret via configuracao (D-05), seed dev-only pendente

- Commit: 6dbc447a4929035cf1bb79c4b3b34a64c356b908
- Date: 2026-10-20

## a772ea2 - feat: PermissionService + /api/permission/check no core; web UI sem ToolExecutor (risk policy na bridge)

- Commit: a772ea23be03686863394a2f441ba35763da6961
- Date: 2026-10-20

## 143e578 - feat: /v1/responses passthrough no proxy do AI.Manager (core)

- Commit: 143e578e316b38d0cbacc4366da7d9e874d5b5db
- Date: 2026-10-20

## eb3ee4a - refactor: ProxyController -> ProxyBridgeController; remove unused Reg; fix duplicate using; fix EntityStatus.Code hiding

- Commit: eb3ee4a83bf29b82c20d77522413e087d5fb1b4c
- Date: 2026-10-20

## 15a2a85 - refactor: ProxyBridgeController -> BindingBridgeController (semantico)

- Commit: 15a2a8570c146f1493c2c93f68c58edd609c5b7a
- Date: 2026-10-20

## 8a3cd8a - feat: Rochas.AI.Manager.Core classlib + NuGet pack + README multi-lingua + LICENSE

- Commit: 8a3cd8aa35895b66fbc854a1293b827d2c178d66
- Date: 2026-10-20

## c5efc7e - refactor: Api/Web referenciam apenas Rochas.AI.Manager.Core (desacoplado)

- Commit: c5efc7e15c3298bdd71ff7e1d305c08ea7795733
- Date: 2026-10-20

## b0f4226 - feat: TrainingJobWorker BackgroundService (fila, claim, cancelamento cooperativo); /run retorna 202

- Commit: b0f4226009aeff61b6297ed78986f8ecbdbbbc79
- Date: 2026-10-20

## 98e28bc - refactor: worker com Serilog structured logging (scope JobId)

- Commit: 98e28bc0ac5103ad7f289daebc43d71edc6c14ce
- Date: 2026-10-20

## 1cece08 - feat: modo diagnostico (--diagnostic/DIAGNOSTIC) no worker; telemetria Rochas.Telemetry integrada

- Commit: 1cece0896a671f2e90854d25703bfbd2aec2e69f
- Date: 2026-10-20


# Git history (rocha83/AI.Manager)

## abba7e7 - initial: Rochas.AI.Manager (full project)

- Commit: abba7e717b32360eb1a79ae731a61e8f1fccf7ea
- Date: 2026-10-20

 Rochas.AI.Manager.slnx                             |   14 +
 assets/gemma-trainer/dataset_prep.py               |   44 +
 assets/gemma-trainer/sft_train.py                  |   31 +
 assets/gemma-trainer/templates/dpo.jsonl           |    2 +
 assets/gemma-trainer/templates/rm.jsonl            |    1 +
 assets/gemma-trainer/templates/sft.jsonl           |    2 +
 docs/ai-manager/ARQUITETURA-CAMADAS.md             |   22 +
 docs/ai-manager/GUIA-INICIANTE.md                  |   56 +
 docs/ai-manager/GUIA-TUNING-EQUIPE-TECNICA.md      |  124 +
 docs/ai-manager/REPORT-PENDINGS.md                 |  274 +
 .../ai-manager/Rochas.AI.Manager-Arquitetura v1.md |  914 ++
 .../ai-manager/Rochas.AI.Manager-Arquitetura v2.md |  524 ++
 .../Controllers/AgentsController.cs                |   21 +
 .../Controllers/BindingBridgeController.cs         |  230 +
 .../Controllers/BridgeCapturesController.cs        |   19 +
 .../Controllers/DatasetsController.cs              |   23 +
 .../Controllers/ExperimentsController.cs           |   24 +
 .../Controllers/HardwareController.cs              |   15 +
 .../Controllers/HarnessAttachmentsController.cs    |   18 +
 .../Controllers/HarnessController.cs               |   32 +
 .../Controllers/HarnessLinterController.cs         |   16 +
 .../Controllers/HarnessProfilesController.cs       |   23 +
 .../Controllers/HarnessRunsController.cs           |   20 +
 .../Controllers/InstructionProfilesController.cs   |   21 +
 .../Controllers/ModelArtifactsController.cs        |   21 +
 .../Controllers/ModelBindingsController.cs         |   22 +
 .../Controllers/PermissionController.cs            |   20 +
 .../Controllers/ReportsController.cs               |   79 +
 .../Controllers/ToolPoliciesController.cs          |   22 +
 .../Controllers/ToolsController.cs                 |   21 +
 .../Controllers/TrainingJobsController.cs          |   26 +
 .../Controllers/TrainingPatternsController.cs      |   19 +
 .../Controllers/TrainingWorkflowsController.cs     |   54 +
 .../Controllers/UsersController.cs                 |   21 +
 src/Rochas.AI.Manager.Api/Program.cs               |  186 +
 .../Rochas.AI.Manager.Api.csproj                   |   23 +
 .../Workers/TrainingJobWorker.cs                   |   68 +
 src/Rochas.AI.Manager.Api/appsettings.json         |   13 +
 .../bin/Debug/net10.0/ClosedXML.dll                |  Bin 0 -> 1438208 bytes
 .../bin/Debug/net10.0/Dapper.dll                   |  Bin 0 -> 251392 bytes
 .../bin/Debug/net10.0/DocumentFormat.OpenXml.dll   |  Bin 0 -> 6025128 bytes
 .../bin/Debug/net10.0/DotLiquid.dll                |  Bin 0 -> 110080 bytes
 .../bin/Debug/net10.0/ExcelDataReader.DataSet.dll  |  Bin 0 -> 14848 bytes
 .../bin/Debug/net10.0/ExcelDataReader.dll          |  Bin 0 -> 209408 bytes
 .../bin/Debug/net10.0/ExcelNumberFormat.dll        |  Bin 0 -> 30720 bytes
 .../bin/Debug/net10.0/Google.Protobuf.dll          |  Bin 0 -> 409368 bytes
 .../bin/Debug/net10.0/HtmlAgilityPack.dll          |  Bin 0 -> 163328 bytes
 .../bin/Debug/net10.0/Humanizer.dll                |  Bin 0 -> 355944 bytes
 .../bin/Debug/net10.0/ICSharpCode.SharpZipLib.dll  |  Bin 0 -> 246272 bytes
 .../bin/Debug/net10.0/Irony.dll                    |  Bin 0 -> 144896 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Black.ttf      |  Bin 0 -> 664748 bytes
 .../Debug/net10.0/LatoFont/Lato-BlackItalic.ttf    |  Bin 0 -> 698264 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Bold.ttf       |  Bin 0 -> 656544 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-BoldItalic.ttf |  Bin 0 -> 698364 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-ExtraBold.ttf  |  Bin 0 -> 613920 bytes
 .../net10.0/LatoFont/Lato-ExtraBoldItalic.ttf      |  Bin 0 -> 671876 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-ExtraLight.ttf |  Bin 0 -> 626624 bytes
 .../net10.0/LatoFont/Lato-ExtraLightItalic.ttf     |  Bin 0 -> 653640 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Italic.ttf     |  Bin 0 -> 722900 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Light.ttf      |  Bin 0 -> 643884 bytes
 .../Debug/net10.0/LatoFont/Lato-LightItalic.ttf    |  Bin 0 -> 657552 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Medium.ttf     |  Bin 0 -> 636396 bytes
 .../Debug/net10.0/LatoFont/Lato-MediumItalic.ttf   |  Bin 0 -> 694924 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Regular.ttf    |  Bin 0 -> 656568 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-SemiBold.ttf   |  Bin 0 -> 668548 bytes
 .../Debug/net10.0/LatoFont/Lato-SemiBoldItalic.ttf |  Bin 0 -> 697364 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Thin.ttf       |  Bin 0 -> 584056 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-ThinItalic.ttf |  Bin 0 -> 608716 bytes
 .../bin/Debug/net10.0/LatoFont/OFL.txt             |   93 +
 ...crosoft.AspNetCore.Authentication.JwtBearer.dll |  Bin 0 -> 52008 bytes
 .../Debug/net10.0/Microsoft.AspNetCore.OpenApi.dll |  Bin 0 -> 193320 bytes
 .../net10.0/Microsoft.Bcl.AsyncInterfaces.dll      |  Bin 0 -> 18696 bytes
 .../Debug/net10.0/Microsoft.Bcl.Cryptography.dll   |  Bin 0 -> 19248 bytes
 .../Microsoft.CodeAnalysis.CSharp.Scripting.dll    |  Bin 0 -> 34568 bytes
 .../Microsoft.CodeAnalysis.CSharp.Workspaces.dll   |  Bin 0 -> 944904 bytes
 .../net10.0/Microsoft.CodeAnalysis.CSharp.dll      |  Bin 0 -> 6839608 bytes
 .../net10.0/Microsoft.CodeAnalysis.Scripting.dll   |  Bin 0 -> 138000 bytes
 ...crosoft.CodeAnalysis.VisualBasic.Workspaces.dll |  Bin 0 -> 810256 bytes
 .../net10.0/Microsoft.CodeAnalysis.VisualBasic.dll |  Bin 0 -> 4856112 bytes
 .../net10.0/Microsoft.CodeAnalysis.Workspaces.dll  |  Bin 0 -> 4230448 bytes
 .../bin/Debug/net10.0/Microsoft.CodeAnalysis.dll   |  Bin 0 -> 3059000 bytes
 ...soft.Data.SqlClient.Extensions.Abstractions.dll |  Bin 0 -> 23448 bytes
 .../Microsoft.Data.SqlClient.Internal.Logging.dll  |  Bin 0 -> 53656 bytes
 .../bin/Debug/net10.0/Microsoft.Data.SqlClient.dll |  Bin 0 -> 93120 bytes
 .../bin/Debug/net10.0/Microsoft.Data.Sqlite.dll    |  Bin 0 -> 177464 bytes
 .../Microsoft.Extensions.DependencyModel.dll       |  Bin 0 -> 85264 bytes
 .../Microsoft.IdentityModel.Abstractions.dll       |  Bin 0 -> 19256 bytes
 .../Microsoft.IdentityModel.JsonWebTokens.dll      |  Bin 0 -> 172344 bytes
 .../net10.0/Microsoft.IdentityModel.Logging.dll    |  Bin 0 -> 38200 bytes
 ...osoft.IdentityModel.Protocols.OpenIdConnect.dll |  Bin 0 -> 137016 bytes
 .../net10.0/Microsoft.IdentityModel.Protocols.dll  |  Bin 0 -> 54112 bytes
 .../net10.0/Microsoft.IdentityModel.Tokens.dll     |  Bin 0 -> 407392 bytes
 .../bin/Debug/net10.0/Microsoft.OpenApi.dll        |  Bin 0 -> 591712 bytes
 .../Debug/net10.0/Microsoft.SqlServer.Server.dll   |  Bin 0 -> 23976 bytes
 .../bin/Debug/net10.0/MySqlConnector.dll           |  Bin 0 -> 880488 bytes
 .../net10.0/NJsonSchema.CodeGeneration.CSharp.dll  |  Bin 0 -> 46592 bytes
 .../Debug/net10.0/NJsonSchema.CodeGeneration.dll   |  Bin 0 -> 32768 bytes
 .../bin/Debug/net10.0/NJsonSchema.dll              |  Bin 0 -> 222208 bytes
 .../bin/Debug/net10.0/Namotion.Reflection.dll      |  Bin 0 -> 47104 bytes
 .../bin/Debug/net10.0/Newtonsoft.Json.dll          |  Bin 0 -> 468480 bytes
 .../bin/Debug/net10.0/Npgsql.dll                   |  Bin 0 -> 1409024 bytes
 .../bin/Debug/net10.0/OpenGraphNet.dll             |  Bin 0 -> 31232 bytes
 .../bin/Debug/net10.0/QRCoder.dll                  |  Bin 0 -> 131584 bytes
 .../bin/Debug/net10.0/QuestPDF.dll                 |  Bin 0 -> 650752 bytes
 .../bin/Debug/net10.0/Rochas.AI.Manager.Api        |  Bin 0 -> 78192 bytes
 .../Debug/net10.0/Rochas.AI.Manager.Api.deps.json  | 2397 +++++
 .../bin/Debug/net10.0/Rochas.AI.Manager.Api.dll    |  Bin 0 -> 186880 bytes
 .../bin/Debug/net10.0/Rochas.AI.Manager.Api.pdb    |  Bin 0 -> 66560 bytes
 .../Rochas.AI.Manager.Api.runtimeconfig.json       |   19 +
 ...s.AI.Manager.Api.staticwebassets.endpoints.json |    1 +
 .../net10.0/Rochas.AI.Manager.Application.dll      |  Bin 0 -> 160256 bytes
 .../net10.0/Rochas.AI.Manager.Application.pdb      |  Bin 0 -> 50980 bytes
 .../bin/Debug/net10.0/Rochas.AI.Manager.Domain.dll |  Bin 0 -> 46080 bytes
 .../bin/Debug/net10.0/Rochas.AI.Manager.Domain.pdb |  Bin 0 -> 26452 bytes
 .../net10.0/Rochas.AI.Manager.Infrastructure.dll   |  Bin 0 -> 49664 bytes
 .../net10.0/Rochas.AI.Manager.Infrastructure.pdb   |  Bin 0 -> 20084 bytes
 .../net10.0/Rochas.AI.Manager.TrainingEngine.dll   |  Bin 0 -> 27136 bytes
 .../net10.0/Rochas.AI.Manager.TrainingEngine.pdb   |  Bin 0 -> 20440 bytes
 .../bin/Debug/net10.0/Rochas.BWOQ.dll              |  Bin 0 -> 57344 bytes
 .../bin/Debug/net10.0/Rochas.DapperRepository.dll  |  Bin 0 -> 71168 bytes
 .../Debug/net10.0/Rochas.Data.Specification.dll    |  Bin 0 -> 13824 bytes
 .../bin/Debug/net10.0/Rochas.ExcelToJsonParser.dll |  Bin 0 -> 36352 bytes
 .../bin/Debug/net10.0/Rochas.Extensions.dll        |  Bin 0 -> 28160 bytes
 .../bin/Debug/net10.0/Rochas.ImageGenerator.dll    |  Bin 0 -> 18944 bytes
 .../bin/Debug/net10.0/Rochas.PDFGenerator.dll      |  Bin 0 -> 3032576 bytes
 .../bin/Debug/net10.0/Rochas.SqlWrapper.dll        |  Bin 0 -> 46080 bytes
 .../bin/Debug/net10.0/Rochas.Telemetry.dll         |  Bin 0 -> 17408 bytes
 .../Debug/net10.0/SQLitePCLRaw.batteries_v2.dll    |  Bin 0 -> 5120 bytes
 .../bin/Debug/net10.0/SQLitePCLRaw.core.dll        |  Bin 0 -> 51200 bytes
 .../net10.0/SQLitePCLRaw.provider.e_sqlite3.dll    |  Bin 0 -> 36864 bytes
 .../bin/Debug/net10.0/Serilog.AspNetCore.dll       |  Bin 0 -> 17920 bytes
 .../Debug/net10.0/Serilog.Extensions.Hosting.dll   |  Bin 0 -> 35840 bytes
 .../Debug/net10.0/Serilog.Extensions.Logging.dll   |  Bin 0 -> 28672 bytes
 .../Debug/net10.0/Serilog.Formatting.Compact.dll   |  Bin 0 -> 9728 bytes
 .../net10.0/Serilog.Settings.Configuration.dll     |  Bin 0 -> 70144 bytes
 .../bin/Debug/net10.0/Serilog.Sinks.Console.dll    |  Bin 0 -> 38400 bytes
 .../bin/Debug/net10.0/Serilog.Sinks.Debug.dll      |  Bin 0 -> 7168 bytes
 .../bin/Debug/net10.0/Serilog.Sinks.File.dll       |  Bin 0 -> 36864 bytes
 .../bin/Debug/net10.0/Serilog.dll                  |  Bin 0 -> 163840 bytes
 .../bin/Debug/net10.0/SixLabors.Fonts.dll          |  Bin 0 -> 1135616 bytes
 .../Debug/net10.0/SixLabors.ImageSharp.Drawing.dll |  Bin 0 -> 173056 bytes
 .../bin/Debug/net10.0/SixLabors.ImageSharp.dll     |  Bin 0 -> 1806848 bytes
 .../bin/Debug/net10.0/SkiaSharp.dll                |  Bin 0 -> 432568 bytes
 .../net10.0/Swashbuckle.AspNetCore.Swagger.dll     |  Bin 0 -> 24064 bytes
 .../net10.0/Swashbuckle.AspNetCore.SwaggerGen.dll  |  Bin 0 -> 161280 bytes
 .../net10.0/Swashbuckle.AspNetCore.SwaggerUI.dll   |  Bin 0 -> 754688 bytes
 .../net10.0/System.Composition.AttributedModel.dll |  Bin 0 -> 20248 bytes
 .../net10.0/System.Composition.Convention.dll      |  Bin 0 -> 54040 bytes
 .../Debug/net10.0/System.Composition.Hosting.dll   |  Bin 0 -> 56072 bytes
 .../Debug/net10.0/System.Composition.Runtime.dll   |  Bin 0 -> 27424 bytes
 .../net10.0/System.Composition.TypedParts.dll      |  Bin 0 -> 65816 bytes
 .../System.Configuration.ConfigurationManager.dll  |  Bin 0 -> 442664 bytes
 .../bin/Debug/net10.0/System.IO.Packaging.dll      |  Bin 0 -> 122480 bytes
 .../net10.0/System.IdentityModel.Tokens.Jwt.dll    |  Bin 0 -> 91960 bytes
 .../bin/Debug/net10.0/System.Linq.Dynamic.Core.dll |  Bin 0 -> 247296 bytes
 .../System.Security.Cryptography.ProtectedData.dll |  Bin 0 -> 38184 bytes
 .../bin/Debug/net10.0/TorchSharp.dll               |  Bin 0 -> 1556920 bytes
 .../bin/Debug/net10.0/XLParser.dll                 |  Bin 0 -> 54784 bytes
 .../bin/Debug/net10.0/ai.manager.db                |  Bin 0 -> 94208 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17160 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17680 bytes
 .../cs/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 444168 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18184 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 302904 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 41224 bytes
 .../cs/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 40200 bytes
 .../cs/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 139672 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17712 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17672 bytes
 .../de/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 474376 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18744 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 319280 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 42760 bytes
 .../de/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41736 bytes
 .../de/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 144792 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17712 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17672 bytes
 .../es/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 463120 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18184 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25352 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 313616 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 42248 bytes
 .../es/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41272 bytes
 .../es/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 141720 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17160 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17680 bytes
 .../fr/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 475920 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18696 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25400 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 320824 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 43320 bytes
 .../fr/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41736 bytes
 .../fr/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 147352 bytes
 .../bin/Debug/net10.0/it/DotLiquid.resources.dll   |  Bin 0 -> 9728 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17160 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17672 bytes
 .../it/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 469768 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18744 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25392 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 318264 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 42808 bytes
 .../it/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41784 bytes
 .../it/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 142272 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17712 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17672 bytes
 .../ja/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 515848 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18696 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 353032 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 45320 bytes
 .../ja/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 44304 bytes
 .../ja/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 156568 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17168 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17680 bytes
 .../ko/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 475920 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18744 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 323848 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 43280 bytes
 .../ko/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41744 bytes
 .../ko/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 144280 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17208 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17712 bytes
 .../pl/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 476432 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18696 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 328464 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 42760 bytes
 .../pl/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41736 bytes
 .../pl/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 144792 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17160 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17712 bytes
 .../Microsoft.CodeAnalysis.CSharp.resources.dll    |  Bin 0 -> 456456 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18184 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 308016 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 42296 bytes
 .../pt-BR/Microsoft.CodeAnalysis.resources.dll     |  Bin 0 -> 40720 bytes
 .../pt-BR/Microsoft.Data.SqlClient.resources.dll   |  Bin 0 -> 138688 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 18192 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 18192 bytes
 .../ru/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 617744 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 19728 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 27400 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 407312 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 47888 bytes
 .../ru/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 49464 bytes
 .../ru/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 186776 bytes
 .../browser-wasm/nativeassets/net9.0/e_sqlite3.a   |  Bin 0 -> 1308854 bytes
 .../runtimes/linux-arm/native/libe_sqlite3.so      |  Bin 0 -> 982224 bytes
 .../runtimes/linux-arm64/native/libQuestPdfSkia.so |  Bin 0 -> 7542864 bytes
 .../runtimes/linux-arm64/native/libe_sqlite3.so    |  Bin 0 -> 1534296 bytes
 .../net10.0/runtimes/linux-arm64/native/libqpdf.so |  Bin 0 -> 3028440 bytes
 .../runtimes/linux-armel/native/libe_sqlite3.so    |  Bin 0 -> 1375344 bytes
 .../runtimes/linux-mips64/native/libe_sqlite3.so   |  Bin 0 -> 1756544 bytes
 .../runtimes/linux-musl-arm/native/libe_sqlite3.so |  Bin 0 -> 1322584 bytes
 .../linux-musl-arm64/native/libe_sqlite3.so        |  Bin 0 -> 1450704 bytes
 .../linux-musl-riscv64/native/libe_sqlite3.so      |  Bin 0 -> 1015888 bytes
 .../linux-musl-s390x/native/libe_sqlite3.so        |  Bin 0 -> 1685752 bytes
 .../linux-musl-x64/native/libQuestPdfSkia.so       |  Bin 0 -> 8338648 bytes
 .../runtimes/linux-musl-x64/native/libe_sqlite3.so |  Bin 0 -> 1445336 bytes
 .../runtimes/linux-musl-x64/native/libqpdf.so      |  Bin 0 -> 3297984 bytes
 .../runtimes/linux-ppc64le/native/libe_sqlite3.so  |  Bin 0 -> 1937408 bytes
 .../runtimes/linux-riscv64/native/libe_sqlite3.so  |  Bin 0 -> 1291896 bytes
 .../runtimes/linux-s390x/native/libe_sqlite3.so    |  Bin 0 -> 1655776 bytes
 .../runtimes/linux-x64/native/libLibTorchSharp.so  |  Bin 0 -> 2399008 bytes
 .../runtimes/linux-x64/native/libQuestPdfSkia.so   |  Bin 0 -> 8104976 bytes
 .../runtimes/linux-x64/native/libe_sqlite3.so      |  Bin 0 -> 1470784 bytes
 .../net10.0/runtimes/linux-x64/native/libqpdf.so   |  Bin 0 -> 3249656 bytes
 .../runtimes/linux-x86/native/libe_sqlite3.so      |  Bin 0 -> 1547984 bytes
 .../maccatalyst-arm64/native/libe_sqlite3.dylib    |  Bin 0 -> 1695264 bytes
 .../maccatalyst-x64/native/libe_sqlite3.dylib      |  Bin 0 -> 1724352 bytes
 .../osx-arm64/native/libLibTorchSharp.dylib        |  Bin 0 -> 2139480 bytes
 .../osx-arm64/native/libQuestPdfSkia.dylib         |  Bin 0 -> 6661112 bytes
 .../runtimes/osx-arm64/native/libe_sqlite3.dylib   |  Bin 0 -> 1696624 bytes
 .../runtimes/osx-arm64/native/libqpdf.dylib        |  Bin 0 -> 2429808 bytes
 .../runtimes/osx-x64/native/libQuestPdfSkia.dylib  |  Bin 0 -> 7289488 bytes
 .../runtimes/osx-x64/native/libe_sqlite3.dylib     |  Bin 0 -> 1724472 bytes
 .../net10.0/runtimes/osx-x64/native/libqpdf.dylib  |  Bin 0 -> 2457208 bytes
 .../net10.0/runtimes/osx/native/libSkiaSharp.dylib |  Bin 0 -> 15563136 bytes
 .../unix/lib/net9.0/Microsoft.Data.SqlClient.dll   |  Bin 0 -> 1670040 bytes
 .../runtimes/win-arm64/native/LibTorchSharp.dll    |  Bin 0 -> 1848760 bytes
 .../native/Microsoft.Data.SqlClient.SNI.dll        |  Bin 0 -> 537496 bytes
 .../runtimes/win-arm64/native/QuestPdfSkia.dll     |  Bin 0 -> 6909440 bytes
 .../runtimes/win-arm64/native/e_sqlite3.dll        |  Bin 0 -> 1814528 bytes
 .../runtimes/win-arm64/native/libSkiaSharp.dll     |  Bin 0 -> 8084920 bytes
 .../net10.0/runtimes/win-arm64/native/qpdf.dll     |  Bin 0 -> 2868736 bytes
 .../runtimes/win-x64/native/LibTorchSharp.dll      |  Bin 0 -> 1324984 bytes
 .../native/Microsoft.Data.SqlClient.SNI.dll        |  Bin 0 -> 555416 bytes
 .../runtimes/win-x64/native/QuestPdfSkia.dll       |  Bin 0 -> 8161280 bytes
 .../net10.0/runtimes/win-x64/native/e_sqlite3.dll  |  Bin 0 -> 1978880 bytes
 .../runtimes/win-x64/native/libSkiaSharp.dll       |  Bin 0 -> 9580984 bytes
 .../Debug/net10.0/runtimes/win-x64/native/qpdf.dll |  Bin 0 -> 4396544 bytes
 .../native/Microsoft.Data.SqlClient.SNI.dll        |  Bin 0 -> 468880 bytes
 .../runtimes/win-x86/native/QuestPdfSkia.dll       |  Bin 0 -> 7379968 bytes
 .../net10.0/runtimes/win-x86/native/e_sqlite3.dll  |  Bin 0 -> 1533952 bytes
 .../runtimes/win-x86/native/libSkiaSharp.dll       |  Bin 0 -> 8139200 bytes
 .../Debug/net10.0/runtimes/win-x86/native/qpdf.dll |  Bin 0 -> 4743680 bytes
 .../win/lib/net9.0/Microsoft.Data.SqlClient.dll    |  Bin 0 -> 1670040 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17680 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17680 bytes
 .../tr/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 452360 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18224 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25392 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 304440 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 41784 bytes
 .../tr/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 40240 bytes
 .../tr/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 135576 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17160 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17680 bytes
 .../Microsoft.CodeAnalysis.CSharp.resources.dll    |  Bin 0 -> 405776 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18192 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 24840 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 279824 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 40200 bytes
 .../zh-Hans/Microsoft.CodeAnalysis.resources.dll   |  Bin 0 -> 37640 bytes
 .../zh-Hans/Microsoft.Data.SqlClient.resources.dll |  Bin 0 -> 122264 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17168 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17720 bytes
 .../Microsoft.CodeAnalysis.CSharp.resources.dll    |  Bin 0 -> 405304 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18184 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 24840 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 278840 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 40248 bytes
 .../zh-Hant/Microsoft.CodeAnalysis.resources.dll   |  Bin 0 -> 38152 bytes
 .../zh-Hant/Microsoft.Data.SqlClient.resources.dll |  Bin 0 -> 123840 bytes
 .../bin/Release/net10.0/ClosedXML.dll              |  Bin 0 -> 1438208 bytes
 .../bin/Release/net10.0/Dapper.dll                 |  Bin 0 -> 251392 bytes
 .../bin/Release/net10.0/DocumentFormat.OpenXml.dll |  Bin 0 -> 6025128 bytes
 .../bin/Release/net10.0/DotLiquid.dll              |  Bin 0 -> 110080 bytes
 .../Release/net10.0/ExcelDataReader.DataSet.dll    |  Bin 0 -> 14848 bytes
 .../bin/Release/net10.0/ExcelDataReader.dll        |  Bin 0 -> 209408 bytes
 .../bin/Release/net10.0/ExcelNumberFormat.dll      |  Bin 0 -> 30720 bytes
 .../bin/Release/net10.0/Google.Protobuf.dll        |  Bin 0 -> 409368 bytes
 .../bin/Release/net10.0/HtmlAgilityPack.dll        |  Bin 0 -> 163328 bytes
 .../bin/Release/net10.0/Humanizer.dll              |  Bin 0 -> 355944 bytes
 .../Release/net10.0/ICSharpCode.SharpZipLib.dll    |  Bin 0 -> 246272 bytes
 .../bin/Release/net10.0/Irony.dll                  |  Bin 0 -> 144896 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Black.ttf    |  Bin 0 -> 664748 bytes
 .../Release/net10.0/LatoFont/Lato-BlackItalic.ttf  |  Bin 0 -> 698264 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Bold.ttf     |  Bin 0 -> 656544 bytes
 .../Release/net10.0/LatoFont/Lato-BoldItalic.ttf   |  Bin 0 -> 698364 bytes
 .../Release/net10.0/LatoFont/Lato-ExtraBold.ttf    |  Bin 0 -> 613920 bytes
 .../net10.0/LatoFont/Lato-ExtraBoldItalic.ttf      |  Bin 0 -> 671876 bytes
 .../Release/net10.0/LatoFont/Lato-ExtraLight.ttf   |  Bin 0 -> 626624 bytes
 .../net10.0/LatoFont/Lato-ExtraLightItalic.ttf     |  Bin 0 -> 653640 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Italic.ttf   |  Bin 0 -> 722900 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Light.ttf    |  Bin 0 -> 643884 bytes
 .../Release/net10.0/LatoFont/Lato-LightItalic.ttf  |  Bin 0 -> 657552 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Medium.ttf   |  Bin 0 -> 636396 bytes
 .../Release/net10.0/LatoFont/Lato-MediumItalic.ttf |  Bin 0 -> 694924 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Regular.ttf  |  Bin 0 -> 656568 bytes
 .../bin/Release/net10.0/LatoFont/Lato-SemiBold.ttf |  Bin 0 -> 668548 bytes
 .../net10.0/LatoFont/Lato-SemiBoldItalic.ttf       |  Bin 0 -> 697364 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Thin.ttf     |  Bin 0 -> 584056 bytes
 .../Release/net10.0/LatoFont/Lato-ThinItalic.ttf   |  Bin 0 -> 608716 bytes
 .../bin/Release/net10.0/LatoFont/OFL.txt           |   93 +
 ...crosoft.AspNetCore.Authentication.JwtBearer.dll |  Bin 0 -> 52008 bytes
 .../net10.0/Microsoft.AspNetCore.OpenApi.dll       |  Bin 0 -> 193320 bytes
 .../net10.0/Microsoft.Bcl.AsyncInterfaces.dll      |  Bin 0 -> 18696 bytes
 .../Release/net10.0/Microsoft.Bcl.Cryptography.dll |  Bin 0 -> 19248 bytes
 .../Microsoft.CodeAnalysis.CSharp.Scripting.dll    |  Bin 0 -> 34568 bytes
 .../Microsoft.CodeAnalysis.CSharp.Workspaces.dll   |  Bin 0 -> 944904 bytes
 .../net10.0/Microsoft.CodeAnalysis.CSharp.dll      |  Bin 0 -> 6839608 bytes
 .../net10.0/Microsoft.CodeAnalysis.Scripting.dll   |  Bin 0 -> 138000 bytes
 ...crosoft.CodeAnalysis.VisualBasic.Workspaces.dll |  Bin 0 -> 810256 bytes
 .../net10.0/Microsoft.CodeAnalysis.VisualBasic.dll |  Bin 0 -> 4856112 bytes
 .../net10.0/Microsoft.CodeAnalysis.Workspaces.dll  |  Bin 0 -> 4230448 bytes
 .../bin/Release/net10.0/Microsoft.CodeAnalysis.dll |  Bin 0 -> 3059000 bytes
 ...soft.Data.SqlClient.Extensions.Abstractions.dll |  Bin 0 -> 23448 bytes
 .../Microsoft.Data.SqlClient.Internal.Logging.dll  |  Bin 0 -> 53656 bytes
 .../Release/net10.0/Microsoft.Data.SqlClient.dll   |  Bin 0 -> 93120 bytes
 .../bin/Release/net10.0/Microsoft.Data.Sqlite.dll  |  Bin 0 -> 177464 bytes
 .../Microsoft.Extensions.DependencyModel.dll       |  Bin 0 -> 85264 bytes
 .../Microsoft.IdentityModel.Abstractions.dll       |  Bin 0 -> 19256 bytes
 .../Microsoft.IdentityModel.JsonWebTokens.dll      |  Bin 0 -> 172344 bytes
 .../net10.0/Microsoft.IdentityModel.Logging.dll    |  Bin 0 -> 38200 bytes
 ...osoft.IdentityModel.Protocols.OpenIdConnect.dll |  Bin 0 -> 137016 bytes
 .../net10.0/Microsoft.IdentityModel.Protocols.dll  |  Bin 0 -> 54112 bytes
 .../net10.0/Microsoft.IdentityModel.Tokens.dll     |  Bin 0 -> 407392 bytes
 .../bin/Release/net10.0/Microsoft.OpenApi.dll      |  Bin 0 -> 591712 bytes
 .../Release/net10.0/Microsoft.SqlServer.Server.dll |  Bin 0 -> 23976 bytes
 .../bin/Release/net10.0/MySqlConnector.dll         |  Bin 0 -> 880488 bytes
 .../net10.0/NJsonSchema.CodeGeneration.CSharp.dll  |  Bin 0 -> 46592 bytes
 .../Release/net10.0/NJsonSchema.CodeGeneration.dll |  Bin 0 -> 32768 bytes
 .../bin/Release/net10.0/NJsonSchema.dll            |  Bin 0 -> 222208 bytes
 .../bin/Release/net10.0/Namotion.Reflection.dll    |  Bin 0 -> 47104 bytes
 .../bin/Release/net10.0/Newtonsoft.Json.dll        |  Bin 0 -> 468480 bytes
 .../bin/Release/net10.0/Npgsql.dll                 |  Bin 0 -> 1409024 bytes
 .../bin/Release/net10.0/OpenGraphNet.dll           |  Bin 0 -> 31232 bytes
 .../bin/Release/net10.0/QRCoder.dll                |  Bin 0 -> 131584 bytes
 .../bin/Release/net10.0/QuestPDF.dll               |  Bin 0 -> 650752 bytes
 .../bin/Release/net10.0/Rochas.AI.Manager.Api      |  Bin 0 -> 78192 bytes
 .../net10.0/Rochas.AI.Manager.Api.deps.json        | 2413 +++++
 .../bin/Release/net10.0/Rochas.AI.Manager.Api.dll  |  Bin 0 -> 189952 bytes
 .../bin/Release/net10.0/Rochas.AI.Manager.Api.pdb  |  Bin 0 -> 64812 bytes
 .../Rochas.AI.Manager.Api.runtimeconfig.json       |   20 +
 ...s.AI.Manager.Api.staticwebassets.endpoints.json |    1 +
 .../net10.0/Rochas.AI.Manager.Application.dll      |  Bin 0 -> 148992 bytes
 .../net10.0/Rochas.AI.Manager.Application.pdb      |  Bin 0 -> 44352 bytes
 .../bin/Release/net10.0/Rochas.AI.Manager.Core.dll |  Bin 0 -> 4608 bytes
 .../bin/Release/net10.0/Rochas.AI.Manager.Core.pdb |  Bin 0 -> 14928 bytes
 .../Release/net10.0/Rochas.AI.Manager.Domain.dll   |  Bin 0 -> 52224 bytes
 .../Release/net10.0/Rochas.AI.Manager.Domain.pdb   |  Bin 0 -> 27016 bytes
 .../net10.0/Rochas.AI.Manager.Infrastructure.dll   |  Bin 0 -> 51200 bytes
 .../net10.0/Rochas.AI.Manager.Infrastructure.pdb   |  Bin 0 -> 20144 bytes
 .../net10.0/Rochas.AI.Manager.TrainingEngine.dll   |  Bin 0 -> 23040 bytes
 .../net10.0/Rochas.AI.Manager.TrainingEngine.pdb   |  Bin 0 -> 19144 bytes
 .../bin/Release/net10.0/Rochas.BWOQ.dll            |  Bin 0 -> 57344 bytes
 .../Release/net10.0/Rochas.DapperRepository.dll    |  Bin 0 -> 71168 bytes
 .../Release/net10.0/Rochas.Data.Specification.dll  |  Bin 0 -> 13824 bytes
 .../Release/net10.0/Rochas.ExcelToJsonParser.dll   |  Bin 0 -> 36352 bytes
 .../bin/Release/net10.0/Rochas.Extensions.dll      |  Bin 0 -> 28160 bytes
 .../bin/Release/net10.0/Rochas.ImageGenerator.dll  |  Bin 0 -> 18944 bytes
 .../bin/Release/net10.0/Rochas.PDFGenerator.dll    |  Bin 0 -> 3032576 bytes
 .../bin/Release/net10.0/Rochas.SqlWrapper.dll      |  Bin 0 -> 46080 bytes
 .../bin/Release/net10.0/Rochas.Telemetry.dll       |  Bin 0 -> 17408 bytes
 .../Release/net10.0/SQLitePCLRaw.batteries_v2.dll  |  Bin 0 -> 5120 bytes
 .../bin/Release/net10.0/SQLitePCLRaw.core.dll      |  Bin 0 -> 51200 bytes
 .../net10.0/SQLitePCLRaw.provider.e_sqlite3.dll    |  Bin 0 -> 36864 bytes
 .../bin/Release/net10.0/Serilog.AspNetCore.dll     |  Bin 0 -> 17920 bytes
 .../Release/net10.0/Serilog.Extensions.Hosting.dll |  Bin 0 -> 35840 bytes
 .../Release/net10.0/Serilog.Extensions.Logging.dll |  Bin 0 -> 28672 bytes
 .../Release/net10.0/Serilog.Formatting.Compact.dll |  Bin 0 -> 9728 bytes
 .../net10.0/Serilog.Settings.Configuration.dll     |  Bin 0 -> 70144 bytes
 .../bin/Release/net10.0/Serilog.Sinks.Console.dll  |  Bin 0 -> 38400 bytes
 .../bin/Release/net10.0/Serilog.Sinks.Debug.dll    |  Bin 0 -> 7168 bytes
 .../bin/Release/net10.0/Serilog.Sinks.File.dll     |  Bin 0 -> 36864 bytes
 .../bin/Release/net10.0/Serilog.dll                |  Bin 0 -> 163840 bytes
 .../bin/Release/net10.0/SixLabors.Fonts.dll        |  Bin 0 -> 1135616 bytes
 .../net10.0/SixLabors.ImageSharp.Drawing.dll       |  Bin 0 -> 173056 bytes
 .../bin/Release/net10.0/SixLabors.ImageSharp.dll   |  Bin 0 -> 1806848 bytes
 .../bin/Release/net10.0/SkiaSharp.dll              |  Bin 0 -> 432568 bytes
 .../net10.0/Swashbuckle.AspNetCore.Swagger.dll     |  Bin 0 -> 24064 bytes
 .../net10.0/Swashbuckle.AspNetCore.SwaggerGen.dll  |  Bin 0 -> 161280 bytes
 .../net10.0/Swashbuckle.AspNetCore.SwaggerUI.dll   |  Bin 0 -> 754688 bytes
 .../net10.0/System.Composition.AttributedModel.dll |  Bin 0 -> 20248 bytes
 .../net10.0/System.Composition.Convention.dll      |  Bin 0 -> 54040 bytes
 .../Release/net10.0/System.Composition.Hosting.dll |  Bin 0 -> 56072 bytes
 .../Release/net10.0/System.Composition.Runtime.dll |  Bin 0 -> 27424 bytes
 .../net10.0/System.Composition.TypedParts.dll      |  Bin 0 -> 65816 bytes
 .../System.Configuration.ConfigurationManager.dll  |  Bin 0 -> 442664 bytes
 .../bin/Release/net10.0/System.IO.Packaging.dll    |  Bin 0 -> 122480 bytes
 .../net10.0/System.IdentityModel.Tokens.Jwt.dll    |  Bin 0 -> 91960 bytes
 .../Release/net10.0/System.Linq.Dynamic.Core.dll   |  Bin 0 -> 247296 bytes
 .../System.Security.Cryptography.ProtectedData.dll |  Bin 0 -> 38184 bytes
 .../bin/Release/net10.0/TorchSharp.dll             |  Bin 0 -> 1556920 bytes
 .../bin/Release/net10.0/XLParser.dll               |  Bin 0 -> 54784 bytes
 .../bin/Release/net10.0/appsettings.json           |   12 +
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17160 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17680 bytes
 .../cs/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 444168 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18184 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 302904 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 41224 bytes
 .../cs/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 40200 bytes
 .../cs/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 139672 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17712 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17672 bytes
 .../de/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 474376 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18744 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 319280 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 42760 bytes
 .../de/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41736 bytes
 .../de/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 144792 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17712 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17672 bytes
 .../es/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 463120 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18184 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25352 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 313616 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 42248 bytes
 .../es/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41272 bytes
 .../es/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 141720 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17160 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17680 bytes
 .../fr/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 475920 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18696 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25400 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 320824 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 43320 bytes
 .../fr/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41736 bytes
 .../fr/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 147352 bytes
 .../bin/Release/net10.0/it/DotLiquid.resources.dll |  Bin 0 -> 9728 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17160 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17672 bytes
 .../it/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 469768 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18744 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25392 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 318264 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 42808 bytes
 .../it/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41784 bytes
 .../it/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 142272 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17712 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17672 bytes
 .../ja/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 515848 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18696 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 353032 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 45320 bytes
 .../ja/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 44304 bytes
 .../ja/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 156568 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17168 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17680 bytes
 .../ko/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 475920 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18744 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 323848 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 43280 bytes
 .../ko/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41744 bytes
 .../ko/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 144280 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17208 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17712 bytes
 .../pl/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 476432 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18696 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 328464 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 42760 bytes
 .../pl/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41736 bytes
 .../pl/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 144792 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17160 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17712 bytes
 .../Microsoft.CodeAnalysis.CSharp.resources.dll    |  Bin 0 -> 456456 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18184 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 308016 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 42296 bytes
 .../pt-BR/Microsoft.CodeAnalysis.resources.dll     |  Bin 0 -> 40720 bytes
 .../pt-BR/Microsoft.Data.SqlClient.resources.dll   |  Bin 0 -> 138688 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 18192 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 18192 bytes
 .../ru/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 617744 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 19728 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 27400 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 407312 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 47888 bytes
 .../ru/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 49464 bytes
 .../ru/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 186776 bytes
 .../browser-wasm/nativeassets/net9.0/e_sqlite3.a   |  Bin 0 -> 1308854 bytes
 .../runtimes/linux-arm/native/libe_sqlite3.so      |  Bin 0 -> 982224 bytes
 .../runtimes/linux-arm64/native/libQuestPdfSkia.so |  Bin 0 -> 7542864 bytes
 .../runtimes/linux-arm64/native/libe_sqlite3.so    |  Bin 0 -> 1534296 bytes
 .../net10.0/runtimes/linux-arm64/native/libqpdf.so |  Bin 0 -> 3028440 bytes
 .../runtimes/linux-armel/native/libe_sqlite3.so    |  Bin 0 -> 1375344 bytes
 .../runtimes/linux-mips64/native/libe_sqlite3.so   |  Bin 0 -> 1756544 bytes
 .../runtimes/linux-musl-arm/native/libe_sqlite3.so |  Bin 0 -> 1322584 bytes
 .../linux-musl-arm64/native/libe_sqlite3.so        |  Bin 0 -> 1450704 bytes
 .../linux-musl-riscv64/native/libe_sqlite3.so      |  Bin 0 -> 1015888 bytes
 .../linux-musl-s390x/native/libe_sqlite3.so        |  Bin 0 -> 1685752 bytes
 .../linux-musl-x64/native/libQuestPdfSkia.so       |  Bin 0 -> 8338648 bytes
 .../runtimes/linux-musl-x64/native/libe_sqlite3.so |  Bin 0 -> 1445336 bytes
 .../runtimes/linux-musl-x64/native/libqpdf.so      |  Bin 0 -> 3297984 bytes
 .../runtimes/linux-ppc64le/native/libe_sqlite3.so  |  Bin 0 -> 1937408 bytes
 .../runtimes/linux-riscv64/native/libe_sqlite3.so  |  Bin 0 -> 1291896 bytes
 .../runtimes/linux-s390x/native/libe_sqlite3.so    |  Bin 0 -> 1655776 bytes
 .../runtimes/linux-x64/native/libLibTorchSharp.so  |  Bin 0 -> 2399008 bytes
 .../runtimes/linux-x64/native/libQuestPdfSkia.so   |  Bin 0 -> 8104976 bytes
 .../runtimes/linux-x64/native/libe_sqlite3.so      |  Bin 0 -> 1470784 bytes
 .../net10.0/runtimes/linux-x64/native/libqpdf.so   |  Bin 0 -> 3249656 bytes
 .../runtimes/linux-x86/native/libe_sqlite3.so      |  Bin 0 -> 1547984 bytes
 .../maccatalyst-arm64/native/libe_sqlite3.dylib    |  Bin 0 -> 1695264 bytes
 .../maccatalyst-x64/native/libe_sqlite3.dylib      |  Bin 0 -> 1724352 bytes
 .../osx-arm64/native/libLibTorchSharp.dylib        |  Bin 0 -> 2139480 bytes
 .../osx-arm64/native/libQuestPdfSkia.dylib         |  Bin 0 -> 6661112 bytes
 .../runtimes/osx-arm64/native/libe_sqlite3.dylib   |  Bin 0 -> 1696624 bytes
 .../runtimes/osx-arm64/native/libqpdf.dylib        |  Bin 0 -> 2429808 bytes
 .../runtimes/osx-x64/native/libQuestPdfSkia.dylib  |  Bin 0 -> 7289488 bytes
 .../runtimes/osx-x64/native/libe_sqlite3.dylib     |  Bin 0 -> 1724472 bytes
 .../net10.0/runtimes/osx-x64/native/libqpdf.dylib  |  Bin 0 -> 2457208 bytes
 .../net10.0/runtimes/osx/native/libSkiaSharp.dylib |  Bin 0 -> 15563136 bytes
 .../unix/lib/net9.0/Microsoft.Data.SqlClient.dll   |  Bin 0 -> 1670040 bytes
 .../runtimes/win-arm64/native/LibTorchSharp.dll    |  Bin 0 -> 1848760 bytes
 .../native/Microsoft.Data.SqlClient.SNI.dll        |  Bin 0 -> 537496 bytes
 .../runtimes/win-arm64/native/QuestPdfSkia.dll     |  Bin 0 -> 6909440 bytes
 .../runtimes/win-arm64/native/e_sqlite3.dll        |  Bin 0 -> 1814528 bytes
 .../runtimes/win-arm64/native/libSkiaSharp.dll     |  Bin 0 -> 8084920 bytes
 .../net10.0/runtimes/win-arm64/native/qpdf.dll     |  Bin 0 -> 2868736 bytes
 .../runtimes/win-x64/native/LibTorchSharp.dll      |  Bin 0 -> 1324984 bytes
 .../native/Microsoft.Data.SqlClient.SNI.dll        |  Bin 0 -> 555416 bytes
 .../runtimes/win-x64/native/QuestPdfSkia.dll       |  Bin 0 -> 8161280 bytes
 .../net10.0/runtimes/win-x64/native/e_sqlite3.dll  |  Bin 0 -> 1978880 bytes
 .../runtimes/win-x64/native/libSkiaSharp.dll       |  Bin 0 -> 9580984 bytes
 .../net10.0/runtimes/win-x64/native/qpdf.dll       |  Bin 0 -> 4396544 bytes
 .../native/Microsoft.Data.SqlClient.SNI.dll        |  Bin 0 -> 468880 bytes
 .../runtimes/win-x86/native/QuestPdfSkia.dll       |  Bin 0 -> 7379968 bytes
 .../net10.0/runtimes/win-x86/native/e_sqlite3.dll  |  Bin 0 -> 1533952 bytes
 .../runtimes/win-x86/native/libSkiaSharp.dll       |  Bin 0 -> 8139200 bytes
 .../net10.0/runtimes/win-x86/native/qpdf.dll       |  Bin 0 -> 4743680 bytes
 .../win/lib/net9.0/Microsoft.Data.SqlClient.dll    |  Bin 0 -> 1670040 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17680 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17680 bytes
 .../tr/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 452360 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18224 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25392 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 304440 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 41784 bytes
 .../tr/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 40240 bytes
 .../tr/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 135576 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17160 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17680 bytes
 .../Microsoft.CodeAnalysis.CSharp.resources.dll    |  Bin 0 -> 405776 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18192 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 24840 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 279824 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 40200 bytes
 .../zh-Hans/Microsoft.CodeAnalysis.resources.dll   |  Bin 0 -> 37640 bytes
 .../zh-Hans/Microsoft.Data.SqlClient.resources.dll |  Bin 0 -> 122264 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17168 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17720 bytes
 .../Microsoft.CodeAnalysis.CSharp.resources.dll    |  Bin 0 -> 405304 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18184 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 24840 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 278840 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 40248 bytes
 .../zh-Hant/Microsoft.CodeAnalysis.resources.dll   |  Bin 0 -> 38152 bytes
 .../zh-Hant/Microsoft.Data.SqlClient.resources.dll |  Bin 0 -> 123840 bytes
 ....NETCoreApp,Version=v10.0.AssemblyAttributes.cs |    4 +
 .../obj/Debug/net10.0/Rochas.A.4E68D655.Up2Date    |    0
 .../net10.0/Rochas.AI.Manager.Api.AssemblyInfo.cs  |   22 +
 .../Rochas.AI.Manager.Api.AssemblyInfoInputs.cache |    1 +
 ...r.Api.GeneratedMSBuildEditorConfig.editorconfig |   32 +
 .../Rochas.AI.Manager.Api.GlobalUsings.g.cs        |   17 +
 ...nager.Api.MvcApplicationPartsAssemblyInfo.cache |    0
 ....Manager.Api.MvcApplicationPartsAssemblyInfo.cs |   17 +
 .../net10.0/Rochas.AI.Manager.Api.assets.cache     |  Bin 0 -> 101768 bytes
 ...s.AI.Manager.Api.csproj.AssemblyReference.cache |  Bin 0 -> 41916 bytes
 ...s.AI.Manager.Api.csproj.CoreCompileInputs.cache |    1 +
 ...chas.AI.Manager.Api.csproj.FileListAbsolute.txt |  313 +
 .../obj/Debug/net10.0/Rochas.AI.Manager.Api.dll    |  Bin 0 -> 186880 bytes
 .../Rochas.AI.Manager.Api.genruntimeconfig.cache   |    1 +
 .../obj/Debug/net10.0/Rochas.AI.Manager.Api.pdb    |  Bin 0 -> 66560 bytes
 .../net10.0/Rochas.AI.Manager.Api.sourcelink.json  |    1 +
 .../obj/Debug/net10.0/apphost                      |  Bin 0 -> 78192 bytes
 .../Debug/net10.0/ref/Rochas.AI.Manager.Api.dll    |  Bin 0 -> 66048 bytes
 .../Debug/net10.0/refint/Rochas.AI.Manager.Api.dll |  Bin 0 -> 66048 bytes
 .../obj/Debug/net10.0/rjsmcshtml.dswa.cache.json   |    1 +
 .../obj/Debug/net10.0/rjsmrazor.dswa.cache.json    |    1 +
 .../net10.0/staticwebassets.build.endpoints.json   |    1 +
 .../obj/Debug/net10.0/staticwebassets.build.json   |    1 +
 .../Debug/net10.0/staticwebassets.build.json.cache |    1 +
 .../obj/Debug/net10.0/swae.build.ex.cache          |    0
 ....NETCoreApp,Version=v10.0.AssemblyAttributes.cs |    4 +
 .../obj/Release/net10.0/Rochas.A.4E68D655.Up2Date  |    0
 .../net10.0/Rochas.AI.Manager.Api.AssemblyInfo.cs  |   22 +
 .../Rochas.AI.Manager.Api.AssemblyInfoInputs.cache |    1 +
 ...r.Api.GeneratedMSBuildEditorConfig.editorconfig |   32 +
 .../Rochas.AI.Manager.Api.GlobalUsings.g.cs        |   17 +
 ...nager.Api.MvcApplicationPartsAssemblyInfo.cache |    0
 ....Manager.Api.MvcApplicationPartsAssemblyInfo.cs |   17 +
 .../net10.0/Rochas.AI.Manager.Api.assets.cache     |  Bin 0 -> 102382 bytes
 ...s.AI.Manager.Api.csproj.AssemblyReference.cache |  Bin 0 -> 42528 bytes
 ...s.AI.Manager.Api.csproj.CoreCompileInputs.cache |    1 +
 ...chas.AI.Manager.Api.csproj.FileListAbsolute.txt |  316 +
 .../obj/Release/net10.0/Rochas.AI.Manager.Api.dll  |  Bin 0 -> 189952 bytes
 .../Rochas.AI.Manager.Api.genruntimeconfig.cache   |    1 +
 .../obj/Release/net10.0/Rochas.AI.Manager.Api.pdb  |  Bin 0 -> 64812 bytes
 .../net10.0/Rochas.AI.Manager.Api.sourcelink.json  |    1 +
 .../obj/Release/net10.0/apphost                    |  Bin 0 -> 78192 bytes
 .../Release/net10.0/ref/Rochas.AI.Manager.Api.dll  |  Bin 0 -> 69632 bytes
 .../net10.0/refint/Rochas.AI.Manager.Api.dll       |  Bin 0 -> 69632 bytes
 .../obj/Release/net10.0/rjsmcshtml.dswa.cache.json |    1 +
 .../obj/Release/net10.0/rjsmrazor.dswa.cache.json  |    1 +
 .../obj/Release/net10.0/rpswa.dswa.cache.json      |    1 +
 .../net10.0/staticwebassets.build.endpoints.json   |    1 +
 .../obj/Release/net10.0/staticwebassets.build.json |    1 +
 .../net10.0/staticwebassets.build.json.cache       |    1 +
 .../obj/Release/net10.0/swae.build.ex.cache        |    0
 .../Rochas.AI.Manager.Api.csproj.nuget.dgspec.json | 2275 +++++
 .../obj/Rochas.AI.Manager.Api.csproj.nuget.g.props |   26 +
 .../Rochas.AI.Manager.Api.csproj.nuget.g.targets   |   12 +
 src/Rochas.AI.Manager.Api/obj/project.assets.json  | 6585 +++++++++++++
 src/Rochas.AI.Manager.Api/obj/project.nuget.cache  |  232 +
 .../Helpers/ReasoningSanitizer.cs                  |   41 +
 .../Helpers/ToolCallCoercer.cs                     |  134 +
 .../Rochas.AI.Manager.Application.csproj           |   19 +
 .../Services/AccuracyEvaluator.cs                  |   38 +
 .../Services/AgentService.cs                       |   53 +
 .../Services/BridgeCaptureService.cs               |   34 +
 .../Services/DatasetService.cs                     |   77 +
 .../Services/ExperimentService.cs                  |   75 +
 .../Services/HardwareService.cs                    |   19 +
 .../Services/HarnessAttachmentService.cs           |   38 +
 .../Services/HarnessLinter.cs                      |   37 +
 .../Services/HarnessProfileService.cs              |   75 +
 .../Services/HarnessRunService.cs                  |   45 +
 .../Services/InstructionProfileService.cs          |   52 +
 .../Services/ModelArtifactService.cs               |   71 +
 .../Services/ModelBindingService.cs                |   54 +
 .../Services/PermissionService.cs                  |   96 +
 .../Services/ToolPolicyService.cs                  |   53 +
 .../Services/TrainingJobService.cs                 |   98 +
 .../Services/TrainingPatternService.cs             |   35 +
 .../Services/TrainingWorkflowService.cs            |  107 +
 .../Services/UserService.cs                        |   48 +
 .../Services/WorkFlowBasedService.cs               |   64 +
 .../Tools/BridgeToolExecutor.cs                    |   54 +
 .../Tools/EditToolHandler.cs                       |   61 +
 .../Tools/FetchToolHandler.cs                      |   53 +
 .../Tools/GitToolHandler.cs                        |   86 +
 .../Tools/GlobToolHandler.cs                       |   67 +
 .../Tools/GrepToolHandler.cs                       |   98 +
 .../Tools/IToolHandler.cs                          |   11 +
 .../Tools/ProcessRunner.cs                         |   77 +
 .../Tools/ReadToolHandler.cs                       |  138 +
 .../Tools/ShellInspector.cs                        |   88 +
 .../Tools/ShellToolHandler.cs                      |  254 +
 .../Tools/SubagentToolHandler.cs                   |  109 +
 .../Tools/ToolContext.cs                           |   10 +
 .../Tools/ToolResult.cs                            |   12 +
 .../Tools/WorkspaceGuard.cs                        |   42 +
 .../Tools/WriteToolHandler.cs                      |   40 +
 .../Rochas.AI.Manager.Application.deps.json        |  900 ++
 .../net10.0/Rochas.AI.Manager.Application.dll      |  Bin 0 -> 165376 bytes
 .../net10.0/Rochas.AI.Manager.Application.pdb      |  Bin 0 -> 52736 bytes
 .../bin/Debug/net10.0/Rochas.AI.Manager.Domain.dll |  Bin 0 -> 53248 bytes
 .../bin/Debug/net10.0/Rochas.AI.Manager.Domain.pdb |  Bin 0 -> 27328 bytes
 .../Rochas.AI.Manager.Application.deps.json        |  900 ++
 .../net10.0/Rochas.AI.Manager.Application.dll      |  Bin 0 -> 148992 bytes
 .../net10.0/Rochas.AI.Manager.Application.pdb      |  Bin 0 -> 44352 bytes
 .../Release/net10.0/Rochas.AI.Manager.Domain.dll   |  Bin 0 -> 52224 bytes
 .../Release/net10.0/Rochas.AI.Manager.Domain.pdb   |  Bin 0 -> 27016 bytes
 ....NETCoreApp,Version=v10.0.AssemblyAttributes.cs |    4 +
 .../obj/Debug/net10.0/Rochas.A.0A30C14D.Up2Date    |    0
 .../Rochas.AI.Manager.Application.AssemblyInfo.cs  |   22 +
 ...AI.Manager.Application.AssemblyInfoInputs.cache |    1 +
 ...ation.GeneratedMSBuildEditorConfig.editorconfig |   18 +
 ...Rochas.AI.Manager.Application.GlobalUsings.g.cs |    8 +
 .../Rochas.AI.Manager.Application.assets.cache     |  Bin 0 -> 31812 bytes
 ...ager.Application.csproj.AssemblyReference.cache |  Bin 0 -> 13671 bytes
 ...ager.Application.csproj.CoreCompileInputs.cache |    1 +
 ...Manager.Application.csproj.FileListAbsolute.txt |   31 +
 .../net10.0/Rochas.AI.Manager.Application.dll      |  Bin 0 -> 165376 bytes
 .../net10.0/Rochas.AI.Manager.Application.pdb      |  Bin 0 -> 52736 bytes
 .../Rochas.AI.Manager.Application.sourcelink.json  |    1 +
 .../net10.0/ref/Rochas.AI.Manager.Application.dll  |  Bin 0 -> 46592 bytes
 .../refint/Rochas.AI.Manager.Application.dll       |  Bin 0 -> 46592 bytes
 ....NETCoreApp,Version=v10.0.AssemblyAttributes.cs |    4 +
 .../obj/Release/net10.0/Rochas.A.0A30C14D.Up2Date  |    0
 .../Rochas.AI.Manager.Application.AssemblyInfo.cs  |   22 +
 ...AI.Manager.Application.AssemblyInfoInputs.cache |    1 +
 ...ation.GeneratedMSBuildEditorConfig.editorconfig |   18 +
 ...Rochas.AI.Manager.Application.GlobalUsings.g.cs |    8 +
 .../Rochas.AI.Manager.Application.assets.cache     |  Bin 0 -> 31812 bytes
 ...ager.Application.csproj.AssemblyReference.cache |  Bin 0 -> 13673 bytes
 ...ager.Application.csproj.CoreCompileInputs.cache |    1 +
 ...Manager.Application.csproj.FileListAbsolute.txt |   16 +
 .../net10.0/Rochas.AI.Manager.Application.dll      |  Bin 0 -> 148992 bytes
 .../net10.0/Rochas.AI.Manager.Application.pdb      |  Bin 0 -> 44352 bytes
 .../Rochas.AI.Manager.Application.sourcelink.json  |    1 +
 .../net10.0/ref/Rochas.AI.Manager.Application.dll  |  Bin 0 -> 47104 bytes
 .../refint/Rochas.AI.Manager.Application.dll       |  Bin 0 -> 47104 bytes
 ...AI.Manager.Application.csproj.nuget.dgspec.json |  698 ++
 ...has.AI.Manager.Application.csproj.nuget.g.props |   15 +
 ...s.AI.Manager.Application.csproj.nuget.g.targets |    8 +
 .../obj/project.assets.json                        | 2323 +++++
 .../obj/project.nuget.cache                        |   73 +
 src/Rochas.AI.Manager.Core/LICENSE                 |   21 +
 src/Rochas.AI.Manager.Core/README.en.md            |   17 +
 src/Rochas.AI.Manager.Core/README.md               |   17 +
 src/Rochas.AI.Manager.Core/README.pt-BR.md         |   17 +
 .../Rochas.AI.Manager.Core.csproj                  |   33 +
 .../bin/Release/Rochas.AI.Manager.Core.1.0.0.nupkg |  Bin 0 -> 4264 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Black.ttf    |  Bin 0 -> 664748 bytes
 .../Release/net10.0/LatoFont/Lato-BlackItalic.ttf  |  Bin 0 -> 698264 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Bold.ttf     |  Bin 0 -> 656544 bytes
 .../Release/net10.0/LatoFont/Lato-BoldItalic.ttf   |  Bin 0 -> 698364 bytes
 .../Release/net10.0/LatoFont/Lato-ExtraBold.ttf    |  Bin 0 -> 613920 bytes
 .../net10.0/LatoFont/Lato-ExtraBoldItalic.ttf      |  Bin 0 -> 671876 bytes
 .../Release/net10.0/LatoFont/Lato-ExtraLight.ttf   |  Bin 0 -> 626624 bytes
 .../net10.0/LatoFont/Lato-ExtraLightItalic.ttf     |  Bin 0 -> 653640 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Italic.ttf   |  Bin 0 -> 722900 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Light.ttf    |  Bin 0 -> 643884 bytes
 .../Release/net10.0/LatoFont/Lato-LightItalic.ttf  |  Bin 0 -> 657552 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Medium.ttf   |  Bin 0 -> 636396 bytes
 .../Release/net10.0/LatoFont/Lato-MediumItalic.ttf |  Bin 0 -> 694924 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Regular.ttf  |  Bin 0 -> 656568 bytes
 .../bin/Release/net10.0/LatoFont/Lato-SemiBold.ttf |  Bin 0 -> 668548 bytes
 .../net10.0/LatoFont/Lato-SemiBoldItalic.ttf       |  Bin 0 -> 697364 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Thin.ttf     |  Bin 0 -> 584056 bytes
 .../Release/net10.0/LatoFont/Lato-ThinItalic.ttf   |  Bin 0 -> 608716 bytes
 .../bin/Release/net10.0/LatoFont/OFL.txt           |   93 +
 .../net10.0/Rochas.AI.Manager.Application.dll      |  Bin 0 -> 148992 bytes
 .../net10.0/Rochas.AI.Manager.Application.pdb      |  Bin 0 -> 44352 bytes
 .../net10.0/Rochas.AI.Manager.Core.deps.json       | 1595 ++++
 .../bin/Release/net10.0/Rochas.AI.Manager.Core.dll |  Bin 0 -> 4608 bytes
 .../bin/Release/net10.0/Rochas.AI.Manager.Core.pdb |  Bin 0 -> 14928 bytes
 .../Release/net10.0/Rochas.AI.Manager.Domain.dll   |  Bin 0 -> 52224 bytes
 .../Release/net10.0/Rochas.AI.Manager.Domain.pdb   |  Bin 0 -> 27016 bytes
 .../net10.0/Rochas.AI.Manager.Infrastructure.dll   |  Bin 0 -> 51200 bytes
 .../net10.0/Rochas.AI.Manager.Infrastructure.pdb   |  Bin 0 -> 20144 bytes
 .../net10.0/Rochas.AI.Manager.TrainingEngine.dll   |  Bin 0 -> 23040 bytes
 .../net10.0/Rochas.AI.Manager.TrainingEngine.pdb   |  Bin 0 -> 19144 bytes
 .../Release/Rochas.AI.Manager.Core.1.0.0.nuspec    |   25 +
 ....NETCoreApp,Version=v10.0.AssemblyAttributes.cs |    4 +
 .../obj/Release/net10.0/Rochas.A.340410B8.Up2Date  |    0
 .../net10.0/Rochas.AI.Manager.Core.AssemblyInfo.cs |   25 +
 ...Rochas.AI.Manager.Core.AssemblyInfoInputs.cache |    1 +
 ....Core.GeneratedMSBuildEditorConfig.editorconfig |   18 +
 .../Rochas.AI.Manager.Core.GlobalUsings.g.cs       |    8 +
 .../net10.0/Rochas.AI.Manager.Core.assets.cache    |  Bin 0 -> 51073 bytes
 ....AI.Manager.Core.csproj.AssemblyReference.cache |  Bin 0 -> 28813 bytes
 ....AI.Manager.Core.csproj.CoreCompileInputs.cache |    1 +
 ...has.AI.Manager.Core.csproj.FileListAbsolute.txt |   41 +
 .../obj/Release/net10.0/Rochas.AI.Manager.Core.dll |  Bin 0 -> 4608 bytes
 .../obj/Release/net10.0/Rochas.AI.Manager.Core.pdb |  Bin 0 -> 14928 bytes
 .../net10.0/Rochas.AI.Manager.Core.sourcelink.json |    1 +
 .../Release/net10.0/ref/Rochas.AI.Manager.Core.dll |  Bin 0 -> 5632 bytes
 .../net10.0/refint/Rochas.AI.Manager.Core.dll      |  Bin 0 -> 5632 bytes
 ...Rochas.AI.Manager.Core.csproj.nuget.dgspec.json | 1751 ++++
 .../Rochas.AI.Manager.Core.csproj.nuget.g.props    |   21 +
 .../Rochas.AI.Manager.Core.csproj.nuget.g.targets  |   10 +
 src/Rochas.AI.Manager.Core/obj/project.assets.json | 3780 ++++++++
 src/Rochas.AI.Manager.Core/obj/project.nuget.cache |  201 +
 src/Rochas.AI.Manager.Domain/Common/IRepository.cs |   13 +
 src/Rochas.AI.Manager.Domain/Common/IUnitOfWork.cs |    9 +
 src/Rochas.AI.Manager.Domain/Datasets/Dataset.cs   |   19 +
 src/Rochas.AI.Manager.Domain/Entities/Agent.cs     |   29 +
 .../Entities/Base/AuditableEntity.cs               |   30 +
 .../Entities/Base/BaseEntity.cs                    |   41 +
 .../Entities/Base/MultiEntityReferrer.cs           |   18 +
 .../Entities/Base/WorkFlowBasedEntity.cs           |   60 +
 .../Entities/Management/EntityStatus.cs            |   13 +
 .../Entities/Management/WorkFlowTransition.cs      |   44 +
 .../Entities/Management/WorkflowStep.cs            |   40 +
 .../Entities/TrainingWorkflowCard.cs               |   25 +
 src/Rochas.AI.Manager.Domain/Entities/User.cs      |   17 +
 .../Experiments/Experiment.cs                      |   21 +
 .../Harness/BridgeCapture.cs                       |   17 +
 .../Harness/HarnessAttachment.cs                   |   19 +
 .../Harness/HarnessProfile.cs                      |   17 +
 .../Harness/HarnessProfileVersion.cs               |   21 +
 src/Rochas.AI.Manager.Domain/Harness/HarnessRun.cs |   27 +
 .../Harness/ModelBinding.cs                        |   21 +
 src/Rochas.AI.Manager.Domain/Harness/ToolPolicy.cs |   21 +
 .../Helpers/EntityHashCode.cs                      |   23 +
 .../Interfaces/Base/IWorkflowBasedEntity.cs        |   18 +
 .../Interfaces/IAccuracyEvaluator.cs               |   10 +
 .../Interfaces/IAgentService.cs                    |   12 +
 .../Interfaces/IBridgeCaptureService.cs            |   10 +
 .../Interfaces/IDatasetService.cs                  |   12 +
 .../Interfaces/IExperimentService.cs               |   14 +
 .../Interfaces/IHardwareService.cs                 |    6 +
 .../Interfaces/IHarnessAttachmentService.cs        |   10 +
 .../Interfaces/IHarnessLinter.cs                   |   10 +
 .../Interfaces/IHarnessProfileService.cs           |   12 +
 .../Interfaces/IHarnessRunService.cs               |   11 +
 .../Interfaces/IInstructionProfileService.cs       |   12 +
 .../Interfaces/IModelArtifactService.cs            |   13 +
 .../Interfaces/IModelBindingService.cs             |   12 +
 .../Interfaces/IPasswordHasher.cs                  |    7 +
 .../Interfaces/IReportService.cs                   |   41 +
 .../Interfaces/IToolPolicyService.cs               |   12 +
 .../Interfaces/ITrainingEngine.cs                  |    6 +
 .../Interfaces/ITrainingJobService.cs              |   14 +
 .../Interfaces/ITrainingPatternService.cs          |   10 +
 .../Interfaces/ITrainingRunner.cs                  |    9 +
 .../Interfaces/ITrainingWorkflowService.cs         |   14 +
 .../Interfaces/IUserService.cs                     |   12 +
 .../Interfaces/ReportQueryException.cs             |   10 +
 .../Interfaces/Services/IWorkFlowBasedService.cs   |   12 +
 .../Models/ModelArtifact.cs                        |   23 +
 .../Profiles/InstructionProfile.cs                 |   17 +
 .../Profiles/TrainingPattern.cs                    |   15 +
 .../Reporting/ReportDefinition.cs                  |   34 +
 .../Rochas.AI.Manager.Domain.csproj                |   12 +
 .../Training/TrainingJob.cs                        |   41 +
 .../net10.0/Rochas.AI.Manager.Domain.deps.json     |   41 +
 .../bin/Debug/net10.0/Rochas.AI.Manager.Domain.dll |  Bin 0 -> 53248 bytes
 .../bin/Debug/net10.0/Rochas.AI.Manager.Domain.pdb |  Bin 0 -> 27328 bytes
 .../net10.0/Rochas.AI.Manager.Domain.deps.json     |   41 +
 .../Release/net10.0/Rochas.AI.Manager.Domain.dll   |  Bin 0 -> 52224 bytes
 .../Release/net10.0/Rochas.AI.Manager.Domain.pdb   |  Bin 0 -> 27016 bytes
 ....NETCoreApp,Version=v10.0.AssemblyAttributes.cs |    4 +
 .../Rochas.AI.Manager.Domain.AssemblyInfo.cs       |   22 +
 ...chas.AI.Manager.Domain.AssemblyInfoInputs.cache |    1 +
 ...omain.GeneratedMSBuildEditorConfig.editorconfig |   18 +
 .../Rochas.AI.Manager.Domain.GlobalUsings.g.cs     |    8 +
 .../net10.0/Rochas.AI.Manager.Domain.assets.cache  |  Bin 0 -> 879 bytes
 ...I.Manager.Domain.csproj.AssemblyReference.cache |  Bin 0 -> 292 bytes
 ...I.Manager.Domain.csproj.CoreCompileInputs.cache |    1 +
 ...s.AI.Manager.Domain.csproj.FileListAbsolute.txt |   24 +
 .../obj/Debug/net10.0/Rochas.AI.Manager.Domain.dll |  Bin 0 -> 53248 bytes
 .../obj/Debug/net10.0/Rochas.AI.Manager.Domain.pdb |  Bin 0 -> 27328 bytes
 .../Rochas.AI.Manager.Domain.sourcelink.json       |    1 +
 .../Debug/net10.0/ref/Rochas.AI.Manager.Domain.dll |  Bin 0 -> 36352 bytes
 .../net10.0/refint/Rochas.AI.Manager.Domain.dll    |  Bin 0 -> 36352 bytes
 ....NETCoreApp,Version=v10.0.AssemblyAttributes.cs |    4 +
 .../Rochas.AI.Manager.Domain.AssemblyInfo.cs       |   22 +
 ...chas.AI.Manager.Domain.AssemblyInfoInputs.cache |    1 +
 ...omain.GeneratedMSBuildEditorConfig.editorconfig |   18 +
 .../Rochas.AI.Manager.Domain.GlobalUsings.g.cs     |    8 +
 .../net10.0/Rochas.AI.Manager.Domain.assets.cache  |  Bin 0 -> 879 bytes
 ...I.Manager.Domain.csproj.AssemblyReference.cache |  Bin 0 -> 292 bytes
 ...I.Manager.Domain.csproj.CoreCompileInputs.cache |    1 +
 ...s.AI.Manager.Domain.csproj.FileListAbsolute.txt |   13 +
 .../Release/net10.0/Rochas.AI.Manager.Domain.dll   |  Bin 0 -> 52224 bytes
 .../Release/net10.0/Rochas.AI.Manager.Domain.pdb   |  Bin 0 -> 27016 bytes
 .../Rochas.AI.Manager.Domain.sourcelink.json       |    1 +
 .../net10.0/ref/Rochas.AI.Manager.Domain.dll       |  Bin 0 -> 36352 bytes
 .../net10.0/refint/Rochas.AI.Manager.Domain.dll    |  Bin 0 -> 36352 bytes
 ...chas.AI.Manager.Domain.csproj.nuget.dgspec.json |  349 +
 .../Rochas.AI.Manager.Domain.csproj.nuget.g.props  |   15 +
 ...Rochas.AI.Manager.Domain.csproj.nuget.g.targets |    2 +
 .../obj/project.assets.json                        |  382 +
 .../obj/project.nuget.cache                        |   10 +
 src/Rochas.AI.Manager.Infrastructure/Data/AppDb.cs |   57 +
 .../Rochas.AI.Manager.Infrastructure.csproj        |   22 +
 .../Services/PasswordHasherService.cs              |   27 +
 .../Services/ReportService.cs                      |  442 +
 .../bin/Debug/net10.0/LatoFont/Lato-Black.ttf      |  Bin 0 -> 664748 bytes
 .../Debug/net10.0/LatoFont/Lato-BlackItalic.ttf    |  Bin 0 -> 698264 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Bold.ttf       |  Bin 0 -> 656544 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-BoldItalic.ttf |  Bin 0 -> 698364 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-ExtraBold.ttf  |  Bin 0 -> 613920 bytes
 .../net10.0/LatoFont/Lato-ExtraBoldItalic.ttf      |  Bin 0 -> 671876 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-ExtraLight.ttf |  Bin 0 -> 626624 bytes
 .../net10.0/LatoFont/Lato-ExtraLightItalic.ttf     |  Bin 0 -> 653640 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Italic.ttf     |  Bin 0 -> 722900 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Light.ttf      |  Bin 0 -> 643884 bytes
 .../Debug/net10.0/LatoFont/Lato-LightItalic.ttf    |  Bin 0 -> 657552 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Medium.ttf     |  Bin 0 -> 636396 bytes
 .../Debug/net10.0/LatoFont/Lato-MediumItalic.ttf   |  Bin 0 -> 694924 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Regular.ttf    |  Bin 0 -> 656568 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-SemiBold.ttf   |  Bin 0 -> 668548 bytes
 .../Debug/net10.0/LatoFont/Lato-SemiBoldItalic.ttf |  Bin 0 -> 697364 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Thin.ttf       |  Bin 0 -> 584056 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-ThinItalic.ttf |  Bin 0 -> 608716 bytes
 .../bin/Debug/net10.0/LatoFont/OFL.txt             |   93 +
 .../net10.0/Rochas.AI.Manager.Application.dll      |  Bin 0 -> 165376 bytes
 .../net10.0/Rochas.AI.Manager.Application.pdb      |  Bin 0 -> 52736 bytes
 .../bin/Debug/net10.0/Rochas.AI.Manager.Domain.dll |  Bin 0 -> 53248 bytes
 .../bin/Debug/net10.0/Rochas.AI.Manager.Domain.pdb |  Bin 0 -> 27328 bytes
 .../Rochas.AI.Manager.Infrastructure.deps.json     | 1426 +++
 .../net10.0/Rochas.AI.Manager.Infrastructure.dll   |  Bin 0 -> 56832 bytes
 .../net10.0/Rochas.AI.Manager.Infrastructure.pdb   |  Bin 0 -> 21384 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Black.ttf    |  Bin 0 -> 664748 bytes
 .../Release/net10.0/LatoFont/Lato-BlackItalic.ttf  |  Bin 0 -> 698264 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Bold.ttf     |  Bin 0 -> 656544 bytes
 .../Release/net10.0/LatoFont/Lato-BoldItalic.ttf   |  Bin 0 -> 698364 bytes
 .../Release/net10.0/LatoFont/Lato-ExtraBold.ttf    |  Bin 0 -> 613920 bytes
 .../net10.0/LatoFont/Lato-ExtraBoldItalic.ttf      |  Bin 0 -> 671876 bytes
 .../Release/net10.0/LatoFont/Lato-ExtraLight.ttf   |  Bin 0 -> 626624 bytes
 .../net10.0/LatoFont/Lato-ExtraLightItalic.ttf     |  Bin 0 -> 653640 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Italic.ttf   |  Bin 0 -> 722900 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Light.ttf    |  Bin 0 -> 643884 bytes
 .../Release/net10.0/LatoFont/Lato-LightItalic.ttf  |  Bin 0 -> 657552 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Medium.ttf   |  Bin 0 -> 636396 bytes
 .../Release/net10.0/LatoFont/Lato-MediumItalic.ttf |  Bin 0 -> 694924 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Regular.ttf  |  Bin 0 -> 656568 bytes
 .../bin/Release/net10.0/LatoFont/Lato-SemiBold.ttf |  Bin 0 -> 668548 bytes
 .../net10.0/LatoFont/Lato-SemiBoldItalic.ttf       |  Bin 0 -> 697364 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Thin.ttf     |  Bin 0 -> 584056 bytes
 .../Release/net10.0/LatoFont/Lato-ThinItalic.ttf   |  Bin 0 -> 608716 bytes
 .../bin/Release/net10.0/LatoFont/OFL.txt           |   93 +
 .../net10.0/Rochas.AI.Manager.Application.dll      |  Bin 0 -> 148992 bytes
 .../net10.0/Rochas.AI.Manager.Application.pdb      |  Bin 0 -> 44352 bytes
 .../Release/net10.0/Rochas.AI.Manager.Domain.dll   |  Bin 0 -> 52224 bytes
 .../Release/net10.0/Rochas.AI.Manager.Domain.pdb   |  Bin 0 -> 27016 bytes
 .../Rochas.AI.Manager.Infrastructure.deps.json     | 1426 +++
 .../net10.0/Rochas.AI.Manager.Infrastructure.dll   |  Bin 0 -> 51200 bytes
 .../net10.0/Rochas.AI.Manager.Infrastructure.pdb   |  Bin 0 -> 20144 bytes
 ....NETCoreApp,Version=v10.0.AssemblyAttributes.cs |    4 +
 .../obj/Debug/net10.0/Rochas.A.54883C40.Up2Date    |    0
 ...ochas.AI.Manager.Infrastructure.AssemblyInfo.cs |   22 +
 ...Manager.Infrastructure.AssemblyInfoInputs.cache |    1 +
 ...cture.GeneratedMSBuildEditorConfig.editorconfig |   18 +
 ...has.AI.Manager.Infrastructure.GlobalUsings.g.cs |    8 +
 .../Rochas.AI.Manager.Infrastructure.assets.cache  |  Bin 0 -> 48032 bytes
 ...r.Infrastructure.csproj.AssemblyReference.cache |  Bin 0 -> 24636 bytes
 ...r.Infrastructure.csproj.CoreCompileInputs.cache |    1 +
 ...ager.Infrastructure.csproj.FileListAbsolute.txt |   54 +
 .../net10.0/Rochas.AI.Manager.Infrastructure.dll   |  Bin 0 -> 56832 bytes
 .../net10.0/Rochas.AI.Manager.Infrastructure.pdb   |  Bin 0 -> 21384 bytes
 ...ochas.AI.Manager.Infrastructure.sourcelink.json |    1 +
 .../ref/Rochas.AI.Manager.Infrastructure.dll       |  Bin 0 -> 9728 bytes
 .../refint/Rochas.AI.Manager.Infrastructure.dll    |  Bin 0 -> 9728 bytes
 ....NETCoreApp,Version=v10.0.AssemblyAttributes.cs |    4 +
 .../obj/Release/net10.0/Rochas.A.54883C40.Up2Date  |    0
 ...ochas.AI.Manager.Infrastructure.AssemblyInfo.cs |   22 +
 ...Manager.Infrastructure.AssemblyInfoInputs.cache |    1 +
 ...cture.GeneratedMSBuildEditorConfig.editorconfig |   18 +
 ...has.AI.Manager.Infrastructure.GlobalUsings.g.cs |    8 +
 .../Rochas.AI.Manager.Infrastructure.assets.cache  |  Bin 0 -> 48032 bytes
 ...r.Infrastructure.csproj.AssemblyReference.cache |  Bin 0 -> 24640 bytes
 ...r.Infrastructure.csproj.CoreCompileInputs.cache |    1 +
 ...ager.Infrastructure.csproj.FileListAbsolute.txt |   37 +
 .../net10.0/Rochas.AI.Manager.Infrastructure.dll   |  Bin 0 -> 51200 bytes
 .../net10.0/Rochas.AI.Manager.Infrastructure.pdb   |  Bin 0 -> 20144 bytes
 ...ochas.AI.Manager.Infrastructure.sourcelink.json |    1 +
 .../ref/Rochas.AI.Manager.Infrastructure.dll       |  Bin 0 -> 10240 bytes
 .../refint/Rochas.AI.Manager.Infrastructure.dll    |  Bin 0 -> 10240 bytes
 ...Manager.Infrastructure.csproj.nuget.dgspec.json | 1058 +++
 ....AI.Manager.Infrastructure.csproj.nuget.g.props |   18 +
 ...I.Manager.Infrastructure.csproj.nuget.g.targets |    9 +
 .../obj/project.assets.json                        | 3449 +++++++
 .../obj/project.nuget.cache                        |  195 +
 .../ITrainingBackend.cs                            |   12 +
 .../Rochas.AI.Manager.TrainingEngine.csproj        |   16 +
 .../TorchSharpTrainingBackend.cs                   |  197 +
 .../TrainingEngine.cs                              |  170 +
 .../TrainingRunner.cs                              |   35 +
 .../net10.0/Rochas.AI.Manager.Application.dll      |  Bin 0 -> 165376 bytes
 .../net10.0/Rochas.AI.Manager.Application.pdb      |  Bin 0 -> 52736 bytes
 .../bin/Debug/net10.0/Rochas.AI.Manager.Domain.dll |  Bin 0 -> 53248 bytes
 .../bin/Debug/net10.0/Rochas.AI.Manager.Domain.pdb |  Bin 0 -> 27328 bytes
 .../Rochas.AI.Manager.TrainingEngine.deps.json     | 1050 +++
 .../net10.0/Rochas.AI.Manager.TrainingEngine.dll   |  Bin 0 -> 27136 bytes
 .../net10.0/Rochas.AI.Manager.TrainingEngine.pdb   |  Bin 0 -> 20436 bytes
 .../net10.0/Rochas.AI.Manager.Application.dll      |  Bin 0 -> 148992 bytes
 .../net10.0/Rochas.AI.Manager.Application.pdb      |  Bin 0 -> 44352 bytes
 .../Release/net10.0/Rochas.AI.Manager.Domain.dll   |  Bin 0 -> 52224 bytes
 .../Release/net10.0/Rochas.AI.Manager.Domain.pdb   |  Bin 0 -> 27016 bytes
 .../Rochas.AI.Manager.TrainingEngine.deps.json     | 1050 +++
 .../net10.0/Rochas.AI.Manager.TrainingEngine.dll   |  Bin 0 -> 23040 bytes
 .../net10.0/Rochas.AI.Manager.TrainingEngine.pdb   |  Bin 0 -> 19144 bytes
 ....NETCoreApp,Version=v10.0.AssemblyAttributes.cs |    4 +
 .../obj/Debug/net10.0/Rochas.A.F991CE25.Up2Date    |    0
 ...ochas.AI.Manager.TrainingEngine.AssemblyInfo.cs |   22 +
 ...Manager.TrainingEngine.AssemblyInfoInputs.cache |    1 +
 ...ngine.GeneratedMSBuildEditorConfig.editorconfig |   18 +
 ...has.AI.Manager.TrainingEngine.GlobalUsings.g.cs |    8 +
 .../Rochas.AI.Manager.TrainingEngine.assets.cache  |  Bin 0 -> 35423 bytes
 ...r.TrainingEngine.csproj.AssemblyReference.cache |  Bin 0 -> 16740 bytes
 ...r.TrainingEngine.csproj.CoreCompileInputs.cache |    1 +
 ...ager.TrainingEngine.csproj.FileListAbsolute.txt |   29 +
 .../net10.0/Rochas.AI.Manager.TrainingEngine.dll   |  Bin 0 -> 27136 bytes
 .../net10.0/Rochas.AI.Manager.TrainingEngine.pdb   |  Bin 0 -> 20436 bytes
 ...ochas.AI.Manager.TrainingEngine.sourcelink.json |    1 +
 .../ref/Rochas.AI.Manager.TrainingEngine.dll       |  Bin 0 -> 8192 bytes
 .../refint/Rochas.AI.Manager.TrainingEngine.dll    |  Bin 0 -> 8192 bytes
 ....NETCoreApp,Version=v10.0.AssemblyAttributes.cs |    4 +
 .../obj/Release/net10.0/Rochas.A.F991CE25.Up2Date  |    0
 ...ochas.AI.Manager.TrainingEngine.AssemblyInfo.cs |   22 +
 ...Manager.TrainingEngine.AssemblyInfoInputs.cache |    1 +
 ...ngine.GeneratedMSBuildEditorConfig.editorconfig |   18 +
 ...has.AI.Manager.TrainingEngine.GlobalUsings.g.cs |    8 +
 .../Rochas.AI.Manager.TrainingEngine.assets.cache  |  Bin 0 -> 35423 bytes
 ...r.TrainingEngine.csproj.AssemblyReference.cache |  Bin 0 -> 16744 bytes
 ...r.TrainingEngine.csproj.CoreCompileInputs.cache |    1 +
 ...ager.TrainingEngine.csproj.FileListAbsolute.txt |   18 +
 .../net10.0/Rochas.AI.Manager.TrainingEngine.dll   |  Bin 0 -> 23040 bytes
 .../net10.0/Rochas.AI.Manager.TrainingEngine.pdb   |  Bin 0 -> 19144 bytes
 ...ochas.AI.Manager.TrainingEngine.sourcelink.json |    1 +
 .../ref/Rochas.AI.Manager.TrainingEngine.dll       |  Bin 0 -> 9216 bytes
 .../refint/Rochas.AI.Manager.TrainingEngine.dll    |  Bin 0 -> 9216 bytes
 ...Manager.TrainingEngine.csproj.nuget.dgspec.json | 1043 +++
 ....AI.Manager.TrainingEngine.csproj.nuget.g.props |   18 +
 ...I.Manager.TrainingEngine.csproj.nuget.g.targets |    9 +
 .../obj/project.assets.json                        | 2641 ++++++
 .../obj/project.nuget.cache                        |   79 +
 .../Controllers/AccountController.cs               |   44 +
 .../Controllers/AgentsController.cs                |   25 +
 .../Controllers/ApprovalsController.cs             |   41 +
 .../Controllers/BridgeCapturesController.cs        |   11 +
 .../Controllers/ChatController.cs                  |   15 +
 .../Controllers/DatasetsController.cs              |   66 +
 .../Controllers/ExperimentsController.cs           |   18 +
 .../Controllers/HarnessController.cs               |   65 +
 .../Controllers/HarnessRunsController.cs           |   11 +
 .../Controllers/InstructionProfilesController.cs   |   18 +
 .../Controllers/ModelArtifactsController.cs        |   32 +
 .../Controllers/ModelBindingsController.cs         |   18 +
 .../Controllers/ReportsController.cs               |   62 +
 .../Controllers/ToolPoliciesController.cs          |   18 +
 .../Controllers/TrainingJobsController.cs          |   31 +
 .../Controllers/TrainingPatternsController.cs      |   18 +
 .../Controllers/TrainingWorkflowController.cs      |   56 +
 .../Controllers/UsersController.cs                 |   25 +
 .../Data/BridgeFeatureSeeder.cs                    |  160 +
 .../Data/HarnessScenarioSeeder.cs                  |   50 +
 src/Rochas.AI.Manager.Web/Data/HarnessSeeder.cs    |   33 +
 .../Data/ScenarioTemplateSeeder.cs                 |   71 +
 src/Rochas.AI.Manager.Web/Data/WorkflowSeeder.cs   |  103 +
 src/Rochas.AI.Manager.Web/Data/harness-nucleo.md   |   31 +
 src/Rochas.AI.Manager.Web/Data/harness-system.md   |  203 +
 src/Rochas.AI.Manager.Web/Data/harness-tools.json  |  400 +
 src/Rochas.AI.Manager.Web/Program.cs               |  261 +
 .../Properties/launchSettings.json                 |   23 +
 .../Rochas.AI.Manager.Web.csproj                   |   33 +
 .../Rochas.AI.Manager.Web.http                     |    6 +
 .../Views/Account/Login.cshtml                     |   22 +
 .../Views/Agents/Index.cshtml                      |   40 +
 .../Views/Approvals/Index.cshtml                   |   57 +
 .../Views/BridgeCaptures/Index.cshtml              |    7 +
 src/Rochas.AI.Manager.Web/Views/Chat/Index.cshtml  | 1499 +++
 .../Views/Datasets/Index.cshtml                    |   33 +
 .../Views/Experiments/Index.cshtml                 |   15 +
 .../Views/Harness/Index.cshtml                     |  118 +
 .../Views/Harness/_ProfileList.cshtml              |   39 +
 .../Views/Harness/_ProfilePreview.cshtml           |   14 +
 .../Views/HarnessRuns/Index.cshtml                 |    8 +
 .../Views/InstructionProfiles/Index.cshtml         |   15 +
 .../Views/ModelArtifacts/Index.cshtml              |   43 +
 .../Views/ModelBindings/Index.cshtml               |   15 +
 .../Views/Reports/Index.cshtml                     |   99 +
 .../Views/Reports/_ReportPreview.cshtml            |   17 +
 .../Views/Shared/_Layout.cshtml                    |  112 +
 .../Views/ToolPolicies/Index.cshtml                |   15 +
 .../Views/TrainingJobs/Index.cshtml                |   59 +
 .../Views/TrainingPatterns/Index.cshtml            |   15 +
 .../Views/TrainingWorkflow/Index.cshtml            |   46 +
 src/Rochas.AI.Manager.Web/Views/Users/Index.cshtml |   39 +
 .../Views/_ViewImports.cshtml                      |    3 +
 src/Rochas.AI.Manager.Web/Views/_ViewStart.cshtml  |    3 +
 src/Rochas.AI.Manager.Web/WeatherForecast.cs       |   12 +
 .../appsettings.Development.json                   |   11 +
 src/Rochas.AI.Manager.Web/appsettings.json         |   12 +
 .../bin/Debug/net10.0/ClosedXML.dll                |  Bin 0 -> 1438208 bytes
 .../bin/Debug/net10.0/Dapper.dll                   |  Bin 0 -> 251392 bytes
 .../bin/Debug/net10.0/Data/harness-tools.json      |  400 +
 .../bin/Debug/net10.0/DocumentFormat.OpenXml.dll   |  Bin 0 -> 6025128 bytes
 .../bin/Debug/net10.0/DotLiquid.dll                |  Bin 0 -> 110080 bytes
 .../bin/Debug/net10.0/ExcelDataReader.DataSet.dll  |  Bin 0 -> 14848 bytes
 .../bin/Debug/net10.0/ExcelDataReader.dll          |  Bin 0 -> 209408 bytes
 .../bin/Debug/net10.0/ExcelNumberFormat.dll        |  Bin 0 -> 30720 bytes
 .../bin/Debug/net10.0/Google.Protobuf.dll          |  Bin 0 -> 409368 bytes
 .../bin/Debug/net10.0/HtmlAgilityPack.dll          |  Bin 0 -> 163328 bytes
 .../bin/Debug/net10.0/Humanizer.dll                |  Bin 0 -> 355944 bytes
 .../bin/Debug/net10.0/ICSharpCode.SharpZipLib.dll  |  Bin 0 -> 246272 bytes
 .../bin/Debug/net10.0/Irony.dll                    |  Bin 0 -> 144896 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Black.ttf      |  Bin 0 -> 664748 bytes
 .../Debug/net10.0/LatoFont/Lato-BlackItalic.ttf    |  Bin 0 -> 698264 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Bold.ttf       |  Bin 0 -> 656544 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-BoldItalic.ttf |  Bin 0 -> 698364 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-ExtraBold.ttf  |  Bin 0 -> 613920 bytes
 .../net10.0/LatoFont/Lato-ExtraBoldItalic.ttf      |  Bin 0 -> 671876 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-ExtraLight.ttf |  Bin 0 -> 626624 bytes
 .../net10.0/LatoFont/Lato-ExtraLightItalic.ttf     |  Bin 0 -> 653640 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Italic.ttf     |  Bin 0 -> 722900 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Light.ttf      |  Bin 0 -> 643884 bytes
 .../Debug/net10.0/LatoFont/Lato-LightItalic.ttf    |  Bin 0 -> 657552 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Medium.ttf     |  Bin 0 -> 636396 bytes
 .../Debug/net10.0/LatoFont/Lato-MediumItalic.ttf   |  Bin 0 -> 694924 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Regular.ttf    |  Bin 0 -> 656568 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-SemiBold.ttf   |  Bin 0 -> 668548 bytes
 .../Debug/net10.0/LatoFont/Lato-SemiBoldItalic.ttf |  Bin 0 -> 697364 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Thin.ttf       |  Bin 0 -> 584056 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-ThinItalic.ttf |  Bin 0 -> 608716 bytes
 .../bin/Debug/net10.0/LatoFont/OFL.txt             |   93 +
 ...crosoft.AspNetCore.Authentication.JwtBearer.dll |  Bin 0 -> 52008 bytes
 .../Debug/net10.0/Microsoft.AspNetCore.OpenApi.dll |  Bin 0 -> 193320 bytes
 .../net10.0/Microsoft.Bcl.AsyncInterfaces.dll      |  Bin 0 -> 18696 bytes
 .../Debug/net10.0/Microsoft.Bcl.Cryptography.dll   |  Bin 0 -> 19248 bytes
 .../Microsoft.CodeAnalysis.CSharp.Scripting.dll    |  Bin 0 -> 34568 bytes
 .../Microsoft.CodeAnalysis.CSharp.Workspaces.dll   |  Bin 0 -> 944904 bytes
 .../net10.0/Microsoft.CodeAnalysis.CSharp.dll      |  Bin 0 -> 6839608 bytes
 .../net10.0/Microsoft.CodeAnalysis.Scripting.dll   |  Bin 0 -> 138000 bytes
 ...crosoft.CodeAnalysis.VisualBasic.Workspaces.dll |  Bin 0 -> 810256 bytes
 .../net10.0/Microsoft.CodeAnalysis.VisualBasic.dll |  Bin 0 -> 4856112 bytes
 .../net10.0/Microsoft.CodeAnalysis.Workspaces.dll  |  Bin 0 -> 4230448 bytes
 .../bin/Debug/net10.0/Microsoft.CodeAnalysis.dll   |  Bin 0 -> 3059000 bytes
 ...soft.Data.SqlClient.Extensions.Abstractions.dll |  Bin 0 -> 23448 bytes
 .../Microsoft.Data.SqlClient.Internal.Logging.dll  |  Bin 0 -> 53656 bytes
 .../bin/Debug/net10.0/Microsoft.Data.SqlClient.dll |  Bin 0 -> 93120 bytes
 .../bin/Debug/net10.0/Microsoft.Data.Sqlite.dll    |  Bin 0 -> 177464 bytes
 .../Microsoft.Extensions.DependencyModel.dll       |  Bin 0 -> 85264 bytes
 .../Microsoft.IdentityModel.Abstractions.dll       |  Bin 0 -> 19256 bytes
 .../Microsoft.IdentityModel.JsonWebTokens.dll      |  Bin 0 -> 172344 bytes
 .../net10.0/Microsoft.IdentityModel.Logging.dll    |  Bin 0 -> 38200 bytes
 ...osoft.IdentityModel.Protocols.OpenIdConnect.dll |  Bin 0 -> 137016 bytes
 .../net10.0/Microsoft.IdentityModel.Protocols.dll  |  Bin 0 -> 54112 bytes
 .../net10.0/Microsoft.IdentityModel.Tokens.dll     |  Bin 0 -> 407392 bytes
 .../bin/Debug/net10.0/Microsoft.OpenApi.dll        |  Bin 0 -> 591712 bytes
 .../Debug/net10.0/Microsoft.SqlServer.Server.dll   |  Bin 0 -> 23976 bytes
 .../bin/Debug/net10.0/MySqlConnector.dll           |  Bin 0 -> 880488 bytes
 .../net10.0/NJsonSchema.CodeGeneration.CSharp.dll  |  Bin 0 -> 46592 bytes
 .../Debug/net10.0/NJsonSchema.CodeGeneration.dll   |  Bin 0 -> 32768 bytes
 .../bin/Debug/net10.0/NJsonSchema.dll              |  Bin 0 -> 222208 bytes
 .../bin/Debug/net10.0/Namotion.Reflection.dll      |  Bin 0 -> 47104 bytes
 .../bin/Debug/net10.0/Newtonsoft.Json.dll          |  Bin 0 -> 468480 bytes
 .../bin/Debug/net10.0/Npgsql.dll                   |  Bin 0 -> 1409024 bytes
 .../bin/Debug/net10.0/OpenGraphNet.dll             |  Bin 0 -> 31232 bytes
 .../bin/Debug/net10.0/QRCoder.dll                  |  Bin 0 -> 131584 bytes
 .../bin/Debug/net10.0/QuestPDF.dll                 |  Bin 0 -> 650752 bytes
 .../net10.0/Rochas.AI.Manager.Application.dll      |  Bin 0 -> 160256 bytes
 .../net10.0/Rochas.AI.Manager.Application.pdb      |  Bin 0 -> 50980 bytes
 .../bin/Debug/net10.0/Rochas.AI.Manager.Domain.dll |  Bin 0 -> 46080 bytes
 .../bin/Debug/net10.0/Rochas.AI.Manager.Domain.pdb |  Bin 0 -> 26452 bytes
 .../net10.0/Rochas.AI.Manager.Infrastructure.dll   |  Bin 0 -> 49664 bytes
 .../net10.0/Rochas.AI.Manager.Infrastructure.pdb   |  Bin 0 -> 20084 bytes
 .../net10.0/Rochas.AI.Manager.TrainingEngine.dll   |  Bin 0 -> 27136 bytes
 .../net10.0/Rochas.AI.Manager.TrainingEngine.pdb   |  Bin 0 -> 20440 bytes
 .../bin/Debug/net10.0/Rochas.AI.Manager.Web        |  Bin 0 -> 78192 bytes
 .../Debug/net10.0/Rochas.AI.Manager.Web.deps.json  | 2394 +++++
 .../bin/Debug/net10.0/Rochas.AI.Manager.Web.dll    |  Bin 0 -> 560128 bytes
 .../bin/Debug/net10.0/Rochas.AI.Manager.Web.pdb    |  Bin 0 -> 162688 bytes
 .../Rochas.AI.Manager.Web.runtimeconfig.json       |   19 +
 ...s.AI.Manager.Web.staticwebassets.endpoints.json |    1 +
 ...has.AI.Manager.Web.staticwebassets.runtime.json |    1 +
 .../bin/Debug/net10.0/Rochas.BWOQ.dll              |  Bin 0 -> 57344 bytes
 .../bin/Debug/net10.0/Rochas.DapperRepository.dll  |  Bin 0 -> 71168 bytes
 .../Debug/net10.0/Rochas.Data.Specification.dll    |  Bin 0 -> 13824 bytes
 .../bin/Debug/net10.0/Rochas.ExcelToJsonParser.dll |  Bin 0 -> 36352 bytes
 .../bin/Debug/net10.0/Rochas.Extensions.dll        |  Bin 0 -> 28160 bytes
 .../bin/Debug/net10.0/Rochas.ImageGenerator.dll    |  Bin 0 -> 18944 bytes
 .../bin/Debug/net10.0/Rochas.PDFGenerator.dll      |  Bin 0 -> 3032576 bytes
 .../bin/Debug/net10.0/Rochas.SqlWrapper.dll        |  Bin 0 -> 46080 bytes
 .../bin/Debug/net10.0/Rochas.Telemetry.dll         |  Bin 0 -> 17408 bytes
 .../Debug/net10.0/SQLitePCLRaw.batteries_v2.dll    |  Bin 0 -> 5120 bytes
 .../bin/Debug/net10.0/SQLitePCLRaw.core.dll        |  Bin 0 -> 51200 bytes
 .../net10.0/SQLitePCLRaw.provider.e_sqlite3.dll    |  Bin 0 -> 36864 bytes
 .../bin/Debug/net10.0/Serilog.AspNetCore.dll       |  Bin 0 -> 17920 bytes
 .../Debug/net10.0/Serilog.Extensions.Hosting.dll   |  Bin 0 -> 35840 bytes
 .../Debug/net10.0/Serilog.Extensions.Logging.dll   |  Bin 0 -> 28672 bytes
 .../Debug/net10.0/Serilog.Formatting.Compact.dll   |  Bin 0 -> 9728 bytes
 .../net10.0/Serilog.Settings.Configuration.dll     |  Bin 0 -> 70144 bytes
 .../bin/Debug/net10.0/Serilog.Sinks.Console.dll    |  Bin 0 -> 38400 bytes
 .../bin/Debug/net10.0/Serilog.Sinks.Debug.dll      |  Bin 0 -> 7168 bytes
 .../bin/Debug/net10.0/Serilog.Sinks.File.dll       |  Bin 0 -> 36864 bytes
 .../bin/Debug/net10.0/Serilog.dll                  |  Bin 0 -> 163840 bytes
 .../bin/Debug/net10.0/SixLabors.Fonts.dll          |  Bin 0 -> 1135616 bytes
 .../Debug/net10.0/SixLabors.ImageSharp.Drawing.dll |  Bin 0 -> 173056 bytes
 .../bin/Debug/net10.0/SixLabors.ImageSharp.dll     |  Bin 0 -> 1806848 bytes
 .../bin/Debug/net10.0/SkiaSharp.dll                |  Bin 0 -> 432568 bytes
 .../net10.0/Swashbuckle.AspNetCore.Swagger.dll     |  Bin 0 -> 24064 bytes
 .../net10.0/Swashbuckle.AspNetCore.SwaggerGen.dll  |  Bin 0 -> 161280 bytes
 .../net10.0/Swashbuckle.AspNetCore.SwaggerUI.dll   |  Bin 0 -> 754688 bytes
 .../net10.0/System.Composition.AttributedModel.dll |  Bin 0 -> 20248 bytes
 .../net10.0/System.Composition.Convention.dll      |  Bin 0 -> 54040 bytes
 .../Debug/net10.0/System.Composition.Hosting.dll   |  Bin 0 -> 56072 bytes
 .../Debug/net10.0/System.Composition.Runtime.dll   |  Bin 0 -> 27424 bytes
 .../net10.0/System.Composition.TypedParts.dll      |  Bin 0 -> 65816 bytes
 .../System.Configuration.ConfigurationManager.dll  |  Bin 0 -> 442664 bytes
 .../bin/Debug/net10.0/System.IO.Packaging.dll      |  Bin 0 -> 122480 bytes
 .../net10.0/System.IdentityModel.Tokens.Jwt.dll    |  Bin 0 -> 91960 bytes
 .../bin/Debug/net10.0/System.Linq.Dynamic.Core.dll |  Bin 0 -> 247296 bytes
 .../System.Security.Cryptography.ProtectedData.dll |  Bin 0 -> 38184 bytes
 .../bin/Debug/net10.0/TorchSharp.dll               |  Bin 0 -> 1556920 bytes
 .../bin/Debug/net10.0/XLParser.dll                 |  Bin 0 -> 54784 bytes
 .../bin/Debug/net10.0/ai.manager.db                |  Bin 0 -> 131072 bytes
 .../bin/Debug/net10.0/appsettings.Development.json |    8 +
 .../bin/Debug/net10.0/appsettings.json             |    9 +
 .../net10.0/assets/gemma-trainer/dataset_prep.py   |   44 +
 .../net10.0/assets/gemma-trainer/sft_train.py      |   31 +
 .../assets/gemma-trainer/templates/dpo.jsonl       |    2 +
 .../assets/gemma-trainer/templates/rm.jsonl        |    1 +
 .../assets/gemma-trainer/templates/sft.jsonl       |    2 +
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17160 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17680 bytes
 .../cs/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 444168 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18184 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 302904 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 41224 bytes
 .../cs/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 40200 bytes
 .../cs/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 139672 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17712 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17672 bytes
 .../de/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 474376 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18744 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 319280 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 42760 bytes
 .../de/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41736 bytes
 .../de/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 144792 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17712 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17672 bytes
 .../es/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 463120 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18184 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25352 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 313616 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 42248 bytes
 .../es/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41272 bytes
 .../es/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 141720 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17160 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17680 bytes
 .../fr/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 475920 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18696 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25400 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 320824 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 43320 bytes
 .../fr/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41736 bytes
 .../fr/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 147352 bytes
 .../bin/Debug/net10.0/it/DotLiquid.resources.dll   |  Bin 0 -> 9728 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17160 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17672 bytes
 .../it/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 469768 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18744 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25392 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 318264 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 42808 bytes
 .../it/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41784 bytes
 .../it/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 142272 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17712 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17672 bytes
 .../ja/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 515848 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18696 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 353032 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 45320 bytes
 .../ja/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 44304 bytes
 .../ja/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 156568 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17168 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17680 bytes
 .../ko/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 475920 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18744 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 323848 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 43280 bytes
 .../ko/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41744 bytes
 .../ko/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 144280 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17208 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17712 bytes
 .../pl/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 476432 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18696 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 328464 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 42760 bytes
 .../pl/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41736 bytes
 .../pl/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 144792 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17160 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17712 bytes
 .../Microsoft.CodeAnalysis.CSharp.resources.dll    |  Bin 0 -> 456456 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18184 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 308016 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 42296 bytes
 .../pt-BR/Microsoft.CodeAnalysis.resources.dll     |  Bin 0 -> 40720 bytes
 .../pt-BR/Microsoft.Data.SqlClient.resources.dll   |  Bin 0 -> 138688 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 18192 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 18192 bytes
 .../ru/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 617744 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 19728 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 27400 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 407312 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 47888 bytes
 .../ru/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 49464 bytes
 .../ru/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 186776 bytes
 .../browser-wasm/nativeassets/net9.0/e_sqlite3.a   |  Bin 0 -> 1308854 bytes
 .../runtimes/linux-arm/native/libe_sqlite3.so      |  Bin 0 -> 982224 bytes
 .../runtimes/linux-arm64/native/libQuestPdfSkia.so |  Bin 0 -> 7542864 bytes
 .../runtimes/linux-arm64/native/libe_sqlite3.so    |  Bin 0 -> 1534296 bytes
 .../net10.0/runtimes/linux-arm64/native/libqpdf.so |  Bin 0 -> 3028440 bytes
 .../runtimes/linux-armel/native/libe_sqlite3.so    |  Bin 0 -> 1375344 bytes
 .../runtimes/linux-mips64/native/libe_sqlite3.so   |  Bin 0 -> 1756544 bytes
 .../runtimes/linux-musl-arm/native/libe_sqlite3.so |  Bin 0 -> 1322584 bytes
 .../linux-musl-arm64/native/libe_sqlite3.so        |  Bin 0 -> 1450704 bytes
 .../linux-musl-riscv64/native/libe_sqlite3.so      |  Bin 0 -> 1015888 bytes
 .../linux-musl-s390x/native/libe_sqlite3.so        |  Bin 0 -> 1685752 bytes
 .../linux-musl-x64/native/libQuestPdfSkia.so       |  Bin 0 -> 8338648 bytes
 .../runtimes/linux-musl-x64/native/libe_sqlite3.so |  Bin 0 -> 1445336 bytes
 .../runtimes/linux-musl-x64/native/libqpdf.so      |  Bin 0 -> 3297984 bytes
 .../runtimes/linux-ppc64le/native/libe_sqlite3.so  |  Bin 0 -> 1937408 bytes
 .../runtimes/linux-riscv64/native/libe_sqlite3.so  |  Bin 0 -> 1291896 bytes
 .../runtimes/linux-s390x/native/libe_sqlite3.so    |  Bin 0 -> 1655776 bytes
 .../runtimes/linux-x64/native/libLibTorchSharp.so  |  Bin 0 -> 2399008 bytes
 .../runtimes/linux-x64/native/libQuestPdfSkia.so   |  Bin 0 -> 8104976 bytes
 .../runtimes/linux-x64/native/libe_sqlite3.so      |  Bin 0 -> 1470784 bytes
 .../net10.0/runtimes/linux-x64/native/libqpdf.so   |  Bin 0 -> 3249656 bytes
 .../runtimes/linux-x86/native/libe_sqlite3.so      |  Bin 0 -> 1547984 bytes
 .../maccatalyst-arm64/native/libe_sqlite3.dylib    |  Bin 0 -> 1695264 bytes
 .../maccatalyst-x64/native/libe_sqlite3.dylib      |  Bin 0 -> 1724352 bytes
 .../osx-arm64/native/libLibTorchSharp.dylib        |  Bin 0 -> 2139480 bytes
 .../osx-arm64/native/libQuestPdfSkia.dylib         |  Bin 0 -> 6661112 bytes
 .../runtimes/osx-arm64/native/libe_sqlite3.dylib   |  Bin 0 -> 1696624 bytes
 .../runtimes/osx-arm64/native/libqpdf.dylib        |  Bin 0 -> 2429808 bytes
 .../runtimes/osx-x64/native/libQuestPdfSkia.dylib  |  Bin 0 -> 7289488 bytes
 .../runtimes/osx-x64/native/libe_sqlite3.dylib     |  Bin 0 -> 1724472 bytes
 .../net10.0/runtimes/osx-x64/native/libqpdf.dylib  |  Bin 0 -> 2457208 bytes
 .../net10.0/runtimes/osx/native/libSkiaSharp.dylib |  Bin 0 -> 15563136 bytes
 .../unix/lib/net9.0/Microsoft.Data.SqlClient.dll   |  Bin 0 -> 1670040 bytes
 .../runtimes/win-arm64/native/LibTorchSharp.dll    |  Bin 0 -> 1848760 bytes
 .../native/Microsoft.Data.SqlClient.SNI.dll        |  Bin 0 -> 537496 bytes
 .../runtimes/win-arm64/native/QuestPdfSkia.dll     |  Bin 0 -> 6909440 bytes
 .../runtimes/win-arm64/native/e_sqlite3.dll        |  Bin 0 -> 1814528 bytes
 .../runtimes/win-arm64/native/libSkiaSharp.dll     |  Bin 0 -> 8084920 bytes
 .../net10.0/runtimes/win-arm64/native/qpdf.dll     |  Bin 0 -> 2868736 bytes
 .../runtimes/win-x64/native/LibTorchSharp.dll      |  Bin 0 -> 1324984 bytes
 .../native/Microsoft.Data.SqlClient.SNI.dll        |  Bin 0 -> 555416 bytes
 .../runtimes/win-x64/native/QuestPdfSkia.dll       |  Bin 0 -> 8161280 bytes
 .../net10.0/runtimes/win-x64/native/e_sqlite3.dll  |  Bin 0 -> 1978880 bytes
 .../runtimes/win-x64/native/libSkiaSharp.dll       |  Bin 0 -> 9580984 bytes
 .../Debug/net10.0/runtimes/win-x64/native/qpdf.dll |  Bin 0 -> 4396544 bytes
 .../native/Microsoft.Data.SqlClient.SNI.dll        |  Bin 0 -> 468880 bytes
 .../runtimes/win-x86/native/QuestPdfSkia.dll       |  Bin 0 -> 7379968 bytes
 .../net10.0/runtimes/win-x86/native/e_sqlite3.dll  |  Bin 0 -> 1533952 bytes
 .../runtimes/win-x86/native/libSkiaSharp.dll       |  Bin 0 -> 8139200 bytes
 .../Debug/net10.0/runtimes/win-x86/native/qpdf.dll |  Bin 0 -> 4743680 bytes
 .../win/lib/net9.0/Microsoft.Data.SqlClient.dll    |  Bin 0 -> 1670040 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17680 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17680 bytes
 .../tr/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 452360 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18224 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25392 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 304440 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 41784 bytes
 .../tr/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 40240 bytes
 .../tr/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 135576 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17160 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17680 bytes
 .../Microsoft.CodeAnalysis.CSharp.resources.dll    |  Bin 0 -> 405776 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18192 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 24840 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 279824 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 40200 bytes
 .../zh-Hans/Microsoft.CodeAnalysis.resources.dll   |  Bin 0 -> 37640 bytes
 .../zh-Hans/Microsoft.Data.SqlClient.resources.dll |  Bin 0 -> 122264 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17168 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17720 bytes
 .../Microsoft.CodeAnalysis.CSharp.resources.dll    |  Bin 0 -> 405304 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18184 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 24840 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 278840 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 40248 bytes
 .../zh-Hant/Microsoft.CodeAnalysis.resources.dll   |  Bin 0 -> 38152 bytes
 .../zh-Hant/Microsoft.Data.SqlClient.resources.dll |  Bin 0 -> 123840 bytes
 .../bin/Release/net10.0/ClosedXML.dll              |  Bin 0 -> 1438208 bytes
 .../bin/Release/net10.0/Dapper.dll                 |  Bin 0 -> 251392 bytes
 .../bin/Release/net10.0/Data/harness-tools.json    |  400 +
 .../bin/Release/net10.0/DocumentFormat.OpenXml.dll |  Bin 0 -> 6025128 bytes
 .../bin/Release/net10.0/DotLiquid.dll              |  Bin 0 -> 110080 bytes
 .../Release/net10.0/ExcelDataReader.DataSet.dll    |  Bin 0 -> 14848 bytes
 .../bin/Release/net10.0/ExcelDataReader.dll        |  Bin 0 -> 209408 bytes
 .../bin/Release/net10.0/ExcelNumberFormat.dll      |  Bin 0 -> 30720 bytes
 .../bin/Release/net10.0/Google.Protobuf.dll        |  Bin 0 -> 409368 bytes
 .../bin/Release/net10.0/HtmlAgilityPack.dll        |  Bin 0 -> 163328 bytes
 .../bin/Release/net10.0/Humanizer.dll              |  Bin 0 -> 355944 bytes
 .../Release/net10.0/ICSharpCode.SharpZipLib.dll    |  Bin 0 -> 246272 bytes
 .../bin/Release/net10.0/Irony.dll                  |  Bin 0 -> 144896 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Black.ttf    |  Bin 0 -> 664748 bytes
 .../Release/net10.0/LatoFont/Lato-BlackItalic.ttf  |  Bin 0 -> 698264 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Bold.ttf     |  Bin 0 -> 656544 bytes
 .../Release/net10.0/LatoFont/Lato-BoldItalic.ttf   |  Bin 0 -> 698364 bytes
 .../Release/net10.0/LatoFont/Lato-ExtraBold.ttf    |  Bin 0 -> 613920 bytes
 .../net10.0/LatoFont/Lato-ExtraBoldItalic.ttf      |  Bin 0 -> 671876 bytes
 .../Release/net10.0/LatoFont/Lato-ExtraLight.ttf   |  Bin 0 -> 626624 bytes
 .../net10.0/LatoFont/Lato-ExtraLightItalic.ttf     |  Bin 0 -> 653640 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Italic.ttf   |  Bin 0 -> 722900 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Light.ttf    |  Bin 0 -> 643884 bytes
 .../Release/net10.0/LatoFont/Lato-LightItalic.ttf  |  Bin 0 -> 657552 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Medium.ttf   |  Bin 0 -> 636396 bytes
 .../Release/net10.0/LatoFont/Lato-MediumItalic.ttf |  Bin 0 -> 694924 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Regular.ttf  |  Bin 0 -> 656568 bytes
 .../bin/Release/net10.0/LatoFont/Lato-SemiBold.ttf |  Bin 0 -> 668548 bytes
 .../net10.0/LatoFont/Lato-SemiBoldItalic.ttf       |  Bin 0 -> 697364 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Thin.ttf     |  Bin 0 -> 584056 bytes
 .../Release/net10.0/LatoFont/Lato-ThinItalic.ttf   |  Bin 0 -> 608716 bytes
 .../bin/Release/net10.0/LatoFont/OFL.txt           |   93 +
 ...crosoft.AspNetCore.Authentication.JwtBearer.dll |  Bin 0 -> 52008 bytes
 .../net10.0/Microsoft.AspNetCore.OpenApi.dll       |  Bin 0 -> 193320 bytes
 .../net10.0/Microsoft.Bcl.AsyncInterfaces.dll      |  Bin 0 -> 18696 bytes
 .../Release/net10.0/Microsoft.Bcl.Cryptography.dll |  Bin 0 -> 19248 bytes
 .../Microsoft.CodeAnalysis.CSharp.Scripting.dll    |  Bin 0 -> 34568 bytes
 .../Microsoft.CodeAnalysis.CSharp.Workspaces.dll   |  Bin 0 -> 944904 bytes
 .../net10.0/Microsoft.CodeAnalysis.CSharp.dll      |  Bin 0 -> 6839608 bytes
 .../net10.0/Microsoft.CodeAnalysis.Scripting.dll   |  Bin 0 -> 138000 bytes
 ...crosoft.CodeAnalysis.VisualBasic.Workspaces.dll |  Bin 0 -> 810256 bytes
 .../net10.0/Microsoft.CodeAnalysis.VisualBasic.dll |  Bin 0 -> 4856112 bytes
 .../net10.0/Microsoft.CodeAnalysis.Workspaces.dll  |  Bin 0 -> 4230448 bytes
 .../bin/Release/net10.0/Microsoft.CodeAnalysis.dll |  Bin 0 -> 3059000 bytes
 ...soft.Data.SqlClient.Extensions.Abstractions.dll |  Bin 0 -> 23448 bytes
 .../Microsoft.Data.SqlClient.Internal.Logging.dll  |  Bin 0 -> 53656 bytes
 .../Release/net10.0/Microsoft.Data.SqlClient.dll   |  Bin 0 -> 93120 bytes
 .../bin/Release/net10.0/Microsoft.Data.Sqlite.dll  |  Bin 0 -> 177464 bytes
 .../Microsoft.Extensions.DependencyModel.dll       |  Bin 0 -> 85264 bytes
 .../Microsoft.IdentityModel.Abstractions.dll       |  Bin 0 -> 19256 bytes
 .../Microsoft.IdentityModel.JsonWebTokens.dll      |  Bin 0 -> 172344 bytes
 .../net10.0/Microsoft.IdentityModel.Logging.dll    |  Bin 0 -> 38200 bytes
 ...osoft.IdentityModel.Protocols.OpenIdConnect.dll |  Bin 0 -> 137016 bytes
 .../net10.0/Microsoft.IdentityModel.Protocols.dll  |  Bin 0 -> 54112 bytes
 .../net10.0/Microsoft.IdentityModel.Tokens.dll     |  Bin 0 -> 407392 bytes
 .../bin/Release/net10.0/Microsoft.OpenApi.dll      |  Bin 0 -> 591712 bytes
 .../Release/net10.0/Microsoft.SqlServer.Server.dll |  Bin 0 -> 23976 bytes
 .../bin/Release/net10.0/MySqlConnector.dll         |  Bin 0 -> 880488 bytes
 .../net10.0/NJsonSchema.CodeGeneration.CSharp.dll  |  Bin 0 -> 46592 bytes
 .../Release/net10.0/NJsonSchema.CodeGeneration.dll |  Bin 0 -> 32768 bytes
 .../bin/Release/net10.0/NJsonSchema.dll            |  Bin 0 -> 222208 bytes
 .../bin/Release/net10.0/Namotion.Reflection.dll    |  Bin 0 -> 47104 bytes
 .../bin/Release/net10.0/Newtonsoft.Json.dll        |  Bin 0 -> 468480 bytes
 .../bin/Release/net10.0/Npgsql.dll                 |  Bin 0 -> 1409024 bytes
 .../bin/Release/net10.0/OpenGraphNet.dll           |  Bin 0 -> 31232 bytes
 .../bin/Release/net10.0/QRCoder.dll                |  Bin 0 -> 131584 bytes
 .../bin/Release/net10.0/QuestPDF.dll               |  Bin 0 -> 650752 bytes
 .../net10.0/Rochas.AI.Manager.Application.dll      |  Bin 0 -> 148992 bytes
 .../net10.0/Rochas.AI.Manager.Application.pdb      |  Bin 0 -> 44356 bytes
 .../bin/Release/net10.0/Rochas.AI.Manager.Core.dll |  Bin 0 -> 4608 bytes
 .../bin/Release/net10.0/Rochas.AI.Manager.Core.pdb |  Bin 0 -> 14928 bytes
 .../Release/net10.0/Rochas.AI.Manager.Domain.dll   |  Bin 0 -> 52224 bytes
 .../Release/net10.0/Rochas.AI.Manager.Domain.pdb   |  Bin 0 -> 27016 bytes
 .../net10.0/Rochas.AI.Manager.Infrastructure.dll   |  Bin 0 -> 51200 bytes
 .../net10.0/Rochas.AI.Manager.Infrastructure.pdb   |  Bin 0 -> 20144 bytes
 .../net10.0/Rochas.AI.Manager.TrainingEngine.dll   |  Bin 0 -> 23040 bytes
 .../net10.0/Rochas.AI.Manager.TrainingEngine.pdb   |  Bin 0 -> 19144 bytes
 .../bin/Release/net10.0/Rochas.AI.Manager.Web      |  Bin 0 -> 78192 bytes
 .../net10.0/Rochas.AI.Manager.Web.deps.json        | 2411 +++++
 .../bin/Release/net10.0/Rochas.AI.Manager.Web.dll  |  Bin 0 -> 525824 bytes
 .../bin/Release/net10.0/Rochas.AI.Manager.Web.pdb  |  Bin 0 -> 151636 bytes
 .../Rochas.AI.Manager.Web.runtimeconfig.json       |   20 +
 ...s.AI.Manager.Web.staticwebassets.endpoints.json |    1 +
 ...has.AI.Manager.Web.staticwebassets.runtime.json |    1 +
 .../bin/Release/net10.0/Rochas.BWOQ.dll            |  Bin 0 -> 57344 bytes
 .../Release/net10.0/Rochas.DapperRepository.dll    |  Bin 0 -> 71168 bytes
 .../Release/net10.0/Rochas.Data.Specification.dll  |  Bin 0 -> 13824 bytes
 .../Release/net10.0/Rochas.ExcelToJsonParser.dll   |  Bin 0 -> 36352 bytes
 .../bin/Release/net10.0/Rochas.Extensions.dll      |  Bin 0 -> 28160 bytes
 .../bin/Release/net10.0/Rochas.ImageGenerator.dll  |  Bin 0 -> 18944 bytes
 .../bin/Release/net10.0/Rochas.PDFGenerator.dll    |  Bin 0 -> 3032576 bytes
 .../bin/Release/net10.0/Rochas.SqlWrapper.dll      |  Bin 0 -> 46080 bytes
 .../bin/Release/net10.0/Rochas.Telemetry.dll       |  Bin 0 -> 17408 bytes
 .../Release/net10.0/SQLitePCLRaw.batteries_v2.dll  |  Bin 0 -> 5120 bytes
 .../bin/Release/net10.0/SQLitePCLRaw.core.dll      |  Bin 0 -> 51200 bytes
 .../net10.0/SQLitePCLRaw.provider.e_sqlite3.dll    |  Bin 0 -> 36864 bytes
 .../bin/Release/net10.0/Serilog.AspNetCore.dll     |  Bin 0 -> 17920 bytes
 .../Release/net10.0/Serilog.Extensions.Hosting.dll |  Bin 0 -> 35840 bytes
 .../Release/net10.0/Serilog.Extensions.Logging.dll |  Bin 0 -> 28672 bytes
 .../Release/net10.0/Serilog.Formatting.Compact.dll |  Bin 0 -> 9728 bytes
 .../net10.0/Serilog.Settings.Configuration.dll     |  Bin 0 -> 70144 bytes
 .../bin/Release/net10.0/Serilog.Sinks.Console.dll  |  Bin 0 -> 38400 bytes
 .../bin/Release/net10.0/Serilog.Sinks.Debug.dll    |  Bin 0 -> 7168 bytes
 .../bin/Release/net10.0/Serilog.Sinks.File.dll     |  Bin 0 -> 36864 bytes
 .../bin/Release/net10.0/Serilog.dll                |  Bin 0 -> 163840 bytes
 .../bin/Release/net10.0/SixLabors.Fonts.dll        |  Bin 0 -> 1135616 bytes
 .../net10.0/SixLabors.ImageSharp.Drawing.dll       |  Bin 0 -> 173056 bytes
 .../bin/Release/net10.0/SixLabors.ImageSharp.dll   |  Bin 0 -> 1806848 bytes
 .../bin/Release/net10.0/SkiaSharp.dll              |  Bin 0 -> 432568 bytes
 .../net10.0/Swashbuckle.AspNetCore.Swagger.dll     |  Bin 0 -> 24064 bytes
 .../net10.0/Swashbuckle.AspNetCore.SwaggerGen.dll  |  Bin 0 -> 161280 bytes
 .../net10.0/Swashbuckle.AspNetCore.SwaggerUI.dll   |  Bin 0 -> 754688 bytes
 .../net10.0/System.Composition.AttributedModel.dll |  Bin 0 -> 20248 bytes
 .../net10.0/System.Composition.Convention.dll      |  Bin 0 -> 54040 bytes
 .../Release/net10.0/System.Composition.Hosting.dll |  Bin 0 -> 56072 bytes
 .../Release/net10.0/System.Composition.Runtime.dll |  Bin 0 -> 27424 bytes
 .../net10.0/System.Composition.TypedParts.dll      |  Bin 0 -> 65816 bytes
 .../System.Configuration.ConfigurationManager.dll  |  Bin 0 -> 442664 bytes
 .../bin/Release/net10.0/System.IO.Packaging.dll    |  Bin 0 -> 122480 bytes
 .../net10.0/System.IdentityModel.Tokens.Jwt.dll    |  Bin 0 -> 91960 bytes
 .../Release/net10.0/System.Linq.Dynamic.Core.dll   |  Bin 0 -> 247296 bytes
 .../System.Security.Cryptography.ProtectedData.dll |  Bin 0 -> 38184 bytes
 .../bin/Release/net10.0/TorchSharp.dll             |  Bin 0 -> 1556920 bytes
 .../bin/Release/net10.0/XLParser.dll               |  Bin 0 -> 54784 bytes
 .../Release/net10.0/appsettings.Development.json   |   11 +
 .../bin/Release/net10.0/appsettings.json           |   12 +
 .../net10.0/assets/gemma-trainer/dataset_prep.py   |   44 +
 .../net10.0/assets/gemma-trainer/sft_train.py      |   31 +
 .../assets/gemma-trainer/templates/dpo.jsonl       |    2 +
 .../assets/gemma-trainer/templates/rm.jsonl        |    1 +
 .../assets/gemma-trainer/templates/sft.jsonl       |    2 +
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17160 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17680 bytes
 .../cs/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 444168 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18184 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 302904 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 41224 bytes
 .../cs/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 40200 bytes
 .../cs/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 139672 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17712 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17672 bytes
 .../de/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 474376 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18744 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 319280 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 42760 bytes
 .../de/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41736 bytes
 .../de/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 144792 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17712 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17672 bytes
 .../es/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 463120 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18184 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25352 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 313616 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 42248 bytes
 .../es/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41272 bytes
 .../es/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 141720 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17160 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17680 bytes
 .../fr/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 475920 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18696 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25400 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 320824 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 43320 bytes
 .../fr/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41736 bytes
 .../fr/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 147352 bytes
 .../bin/Release/net10.0/it/DotLiquid.resources.dll |  Bin 0 -> 9728 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17160 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17672 bytes
 .../it/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 469768 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18744 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25392 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 318264 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 42808 bytes
 .../it/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41784 bytes
 .../it/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 142272 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17712 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17672 bytes
 .../ja/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 515848 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18696 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 353032 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 45320 bytes
 .../ja/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 44304 bytes
 .../ja/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 156568 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17168 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17680 bytes
 .../ko/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 475920 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18744 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 323848 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 43280 bytes
 .../ko/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41744 bytes
 .../ko/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 144280 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17208 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17712 bytes
 .../pl/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 476432 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18696 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 328464 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 42760 bytes
 .../pl/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 41736 bytes
 .../pl/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 144792 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17160 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17712 bytes
 .../Microsoft.CodeAnalysis.CSharp.resources.dll    |  Bin 0 -> 456456 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18184 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25360 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 308016 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 42296 bytes
 .../pt-BR/Microsoft.CodeAnalysis.resources.dll     |  Bin 0 -> 40720 bytes
 .../pt-BR/Microsoft.Data.SqlClient.resources.dll   |  Bin 0 -> 138688 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 18192 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 18192 bytes
 .../ru/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 617744 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 19728 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 27400 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 407312 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 47888 bytes
 .../ru/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 49464 bytes
 .../ru/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 186776 bytes
 .../browser-wasm/nativeassets/net9.0/e_sqlite3.a   |  Bin 0 -> 1308854 bytes
 .../runtimes/linux-arm/native/libe_sqlite3.so      |  Bin 0 -> 982224 bytes
 .../runtimes/linux-arm64/native/libQuestPdfSkia.so |  Bin 0 -> 7542864 bytes
 .../runtimes/linux-arm64/native/libe_sqlite3.so    |  Bin 0 -> 1534296 bytes
 .../net10.0/runtimes/linux-arm64/native/libqpdf.so |  Bin 0 -> 3028440 bytes
 .../runtimes/linux-armel/native/libe_sqlite3.so    |  Bin 0 -> 1375344 bytes
 .../runtimes/linux-mips64/native/libe_sqlite3.so   |  Bin 0 -> 1756544 bytes
 .../runtimes/linux-musl-arm/native/libe_sqlite3.so |  Bin 0 -> 1322584 bytes
 .../linux-musl-arm64/native/libe_sqlite3.so        |  Bin 0 -> 1450704 bytes
 .../linux-musl-riscv64/native/libe_sqlite3.so      |  Bin 0 -> 1015888 bytes
 .../linux-musl-s390x/native/libe_sqlite3.so        |  Bin 0 -> 1685752 bytes
 .../linux-musl-x64/native/libQuestPdfSkia.so       |  Bin 0 -> 8338648 bytes
 .../runtimes/linux-musl-x64/native/libe_sqlite3.so |  Bin 0 -> 1445336 bytes
 .../runtimes/linux-musl-x64/native/libqpdf.so      |  Bin 0 -> 3297984 bytes
 .../runtimes/linux-ppc64le/native/libe_sqlite3.so  |  Bin 0 -> 1937408 bytes
 .../runtimes/linux-riscv64/native/libe_sqlite3.so  |  Bin 0 -> 1291896 bytes
 .../runtimes/linux-s390x/native/libe_sqlite3.so    |  Bin 0 -> 1655776 bytes
 .../runtimes/linux-x64/native/libLibTorchSharp.so  |  Bin 0 -> 2399008 bytes
 .../runtimes/linux-x64/native/libQuestPdfSkia.so   |  Bin 0 -> 8104976 bytes
 .../runtimes/linux-x64/native/libe_sqlite3.so      |  Bin 0 -> 1470784 bytes
 .../net10.0/runtimes/linux-x64/native/libqpdf.so   |  Bin 0 -> 3249656 bytes
 .../runtimes/linux-x86/native/libe_sqlite3.so      |  Bin 0 -> 1547984 bytes
 .../maccatalyst-arm64/native/libe_sqlite3.dylib    |  Bin 0 -> 1695264 bytes
 .../maccatalyst-x64/native/libe_sqlite3.dylib      |  Bin 0 -> 1724352 bytes
 .../osx-arm64/native/libLibTorchSharp.dylib        |  Bin 0 -> 2139480 bytes
 .../osx-arm64/native/libQuestPdfSkia.dylib         |  Bin 0 -> 6661112 bytes
 .../runtimes/osx-arm64/native/libe_sqlite3.dylib   |  Bin 0 -> 1696624 bytes
 .../runtimes/osx-arm64/native/libqpdf.dylib        |  Bin 0 -> 2429808 bytes
 .../runtimes/osx-x64/native/libQuestPdfSkia.dylib  |  Bin 0 -> 7289488 bytes
 .../runtimes/osx-x64/native/libe_sqlite3.dylib     |  Bin 0 -> 1724472 bytes
 .../net10.0/runtimes/osx-x64/native/libqpdf.dylib  |  Bin 0 -> 2457208 bytes
 .../net10.0/runtimes/osx/native/libSkiaSharp.dylib |  Bin 0 -> 15563136 bytes
 .../unix/lib/net9.0/Microsoft.Data.SqlClient.dll   |  Bin 0 -> 1670040 bytes
 .../runtimes/win-arm64/native/LibTorchSharp.dll    |  Bin 0 -> 1848760 bytes
 .../native/Microsoft.Data.SqlClient.SNI.dll        |  Bin 0 -> 537496 bytes
 .../runtimes/win-arm64/native/QuestPdfSkia.dll     |  Bin 0 -> 6909440 bytes
 .../runtimes/win-arm64/native/e_sqlite3.dll        |  Bin 0 -> 1814528 bytes
 .../runtimes/win-arm64/native/libSkiaSharp.dll     |  Bin 0 -> 8084920 bytes
 .../net10.0/runtimes/win-arm64/native/qpdf.dll     |  Bin 0 -> 2868736 bytes
 .../runtimes/win-x64/native/LibTorchSharp.dll      |  Bin 0 -> 1324984 bytes
 .../native/Microsoft.Data.SqlClient.SNI.dll        |  Bin 0 -> 555416 bytes
 .../runtimes/win-x64/native/QuestPdfSkia.dll       |  Bin 0 -> 8161280 bytes
 .../net10.0/runtimes/win-x64/native/e_sqlite3.dll  |  Bin 0 -> 1978880 bytes
 .../runtimes/win-x64/native/libSkiaSharp.dll       |  Bin 0 -> 9580984 bytes
 .../net10.0/runtimes/win-x64/native/qpdf.dll       |  Bin 0 -> 4396544 bytes
 .../native/Microsoft.Data.SqlClient.SNI.dll        |  Bin 0 -> 468880 bytes
 .../runtimes/win-x86/native/QuestPdfSkia.dll       |  Bin 0 -> 7379968 bytes
 .../net10.0/runtimes/win-x86/native/e_sqlite3.dll  |  Bin 0 -> 1533952 bytes
 .../runtimes/win-x86/native/libSkiaSharp.dll       |  Bin 0 -> 8139200 bytes
 .../net10.0/runtimes/win-x86/native/qpdf.dll       |  Bin 0 -> 4743680 bytes
 .../win/lib/net9.0/Microsoft.Data.SqlClient.dll    |  Bin 0 -> 1670040 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17680 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17680 bytes
 .../tr/Microsoft.CodeAnalysis.CSharp.resources.dll |  Bin 0 -> 452360 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18224 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 25392 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 304440 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 41784 bytes
 .../tr/Microsoft.CodeAnalysis.resources.dll        |  Bin 0 -> 40240 bytes
 .../tr/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 135576 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17160 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17680 bytes
 .../Microsoft.CodeAnalysis.CSharp.resources.dll    |  Bin 0 -> 405776 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18192 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 24840 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 279824 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 40200 bytes
 .../zh-Hans/Microsoft.CodeAnalysis.resources.dll   |  Bin 0 -> 37640 bytes
 .../zh-Hans/Microsoft.Data.SqlClient.resources.dll |  Bin 0 -> 122264 bytes
 ...oft.CodeAnalysis.CSharp.Scripting.resources.dll |  Bin 0 -> 17168 bytes
 ...ft.CodeAnalysis.CSharp.Workspaces.resources.dll |  Bin 0 -> 17720 bytes
 .../Microsoft.CodeAnalysis.CSharp.resources.dll    |  Bin 0 -> 405304 bytes
 .../Microsoft.CodeAnalysis.Scripting.resources.dll |  Bin 0 -> 18184 bytes
 ...deAnalysis.VisualBasic.Workspaces.resources.dll |  Bin 0 -> 24840 bytes
 ...icrosoft.CodeAnalysis.VisualBasic.resources.dll |  Bin 0 -> 278840 bytes
 ...Microsoft.CodeAnalysis.Workspaces.resources.dll |  Bin 0 -> 40248 bytes
 .../zh-Hant/Microsoft.CodeAnalysis.resources.dll   |  Bin 0 -> 38152 bytes
 .../zh-Hant/Microsoft.Data.SqlClient.resources.dll |  Bin 0 -> 123840 bytes
 ....NETCoreApp,Version=v10.0.AssemblyAttributes.cs |    4 +
 .../obj/Debug/net10.0/Rochas.A.02DCA53C.Up2Date    |    0
 .../net10.0/Rochas.AI.Manager.Web.AssemblyInfo.cs  |   22 +
 .../Rochas.AI.Manager.Web.AssemblyInfoInputs.cache |    1 +
 ...r.Web.GeneratedMSBuildEditorConfig.editorconfig |  128 +
 .../Rochas.AI.Manager.Web.GlobalUsings.g.cs        |   17 +
 ...nager.Web.MvcApplicationPartsAssemblyInfo.cache |    0
 ....Manager.Web.MvcApplicationPartsAssemblyInfo.cs |   17 +
 .../Rochas.AI.Manager.Web.RazorAssemblyInfo.cache  |    1 +
 .../Rochas.AI.Manager.Web.RazorAssemblyInfo.cs     |   17 +
 .../net10.0/Rochas.AI.Manager.Web.assets.cache     |  Bin 0 -> 101581 bytes
 ...s.AI.Manager.Web.csproj.AssemblyReference.cache |  Bin 0 -> 41916 bytes
 ...s.AI.Manager.Web.csproj.CoreCompileInputs.cache |    1 +
 ...chas.AI.Manager.Web.csproj.FileListAbsolute.txt |  393 +
 .../obj/Debug/net10.0/Rochas.AI.Manager.Web.dll    |  Bin 0 -> 560128 bytes
 .../Rochas.AI.Manager.Web.genruntimeconfig.cache   |    1 +
 .../obj/Debug/net10.0/Rochas.AI.Manager.Web.pdb    |  Bin 0 -> 162688 bytes
 .../net10.0/Rochas.AI.Manager.Web.sourcelink.json  |    1 +
 .../obj/Debug/net10.0/apphost                      |  Bin 0 -> 78192 bytes
 .../16lphm3rhe-{0}-fafqr8z9s7-fafqr8z9s7.gz        |  Bin 0 -> 4140 bytes
 .../25fvasn163-{0}-lfsxvok1dn-lfsxvok1dn.gz        |  Bin 0 -> 16906 bytes
 .../2eh83wy9kb-{0}-wjhuarmh55-wjhuarmh55.gz        |  Bin 0 -> 22536 bytes
 .../360rvt29bx-{0}-oheyjv9vpw-oheyjv9vpw.gz        |  Bin 0 -> 572 bytes
 .../3ixoay1p69-{0}-biywkp7jh7-biywkp7jh7.gz        |  Bin 0 -> 46077 bytes
 .../8c1d0422am-{0}-t5t3pc0fe7-t5t3pc0fe7.gz        |  Bin 0 -> 615 bytes
 .../8zijuhpgoa-{0}-1mm2lhid9t-1mm2lhid9t.gz        |  Bin 0 -> 3968 bytes
 .../a0y2led1js-{0}-nhm0lce0yj-nhm0lce0yj.gz        |  Bin 0 -> 9792 bytes
 .../bczd72z3ee-{0}-cc2kkbutuo-cc2kkbutuo.gz        |  Bin 0 -> 1867 bytes
 .../ch74w8afyx-{0}-pop50kg9ac-pop50kg9ac.gz        |  Bin 0 -> 44941 bytes
 .../g7nhtd75vp-{0}-9p7e1q840y-9p7e1q840y.gz        |  Bin 0 -> 1296 bytes
 .../jeoy3jp7at-{0}-urb3ug61ft-urb3ug61ft.gz        |  Bin 0 -> 368 bytes
 .../l5ezfu96tp-{0}-5014gehn0y-5014gehn0y.gz        |  Bin 0 -> 2508 bytes
 .../mnoywah08s-{0}-miaj7kt92n-miaj7kt92n.gz        |  Bin 0 -> 576 bytes
 .../p3c6yolfvw-{0}-aynrw99unf-aynrw99unf.gz        |  Bin 0 -> 1886 bytes
 .../q3hmrgwz0w-{0}-ulujjfsrlu-ulujjfsrlu.gz        |  Bin 0 -> 20486 bytes
 .../qemdghldm1-{0}-y091c9ksd4-y091c9ksd4.gz        |  Bin 0 -> 3294 bytes
 .../qpnx9z64kh-{0}-z7fubto1m4-z7fubto1m4.gz        |  Bin 0 -> 8005 bytes
 .../rqqsvppg5c-{0}-9pemnltd66-9pemnltd66.gz        |  Bin 0 -> 8691 bytes
 .../vy1j1k3bei-{0}-34naviyjz7-34naviyjz7.gz        |  Bin 0 -> 41540 bytes
 .../wpz2aeb0zz-{0}-860w3lioxn-860w3lioxn.gz        |  Bin 0 -> 2535 bytes
 .../wu2nf6c6fy-{0}-17btfpzddq-17btfpzddq.gz        |  Bin 0 -> 107402 bytes
 .../xdd2z0sun9-{0}-4dtgqqhnb2-4dtgqqhnb2.gz        |  Bin 0 -> 636 bytes
 .../yaelx30t1d-{0}-wc1i5xp98b-wc1i5xp98b.gz        |  Bin 0 -> 10073 bytes
 .../ym1gaa9l26-{0}-oz2w2imawu-oz2w2imawu.gz        |  Bin 0 -> 3348 bytes
 .../obj/Debug/net10.0/rbcswa.dswa.cache.json       |    1 +
 .../Debug/net10.0/ref/Rochas.AI.Manager.Web.dll    |  Bin 0 -> 71168 bytes
 .../Debug/net10.0/refint/Rochas.AI.Manager.Web.dll |  Bin 0 -> 71168 bytes
 .../obj/Debug/net10.0/rjimswa.dswa.cache.json      |    1 +
 .../obj/Debug/net10.0/rjsmcshtml.dswa.cache.json   |    1 +
 .../obj/Debug/net10.0/rjsmrazor.dswa.cache.json    |    1 +
 .../obj/Debug/net10.0/rpswa.dswa.cache.json        |    1 +
 .../net10.0/staticwebassets.build.endpoints.json   |    1 +
 .../obj/Debug/net10.0/staticwebassets.build.json   |    1 +
 .../Debug/net10.0/staticwebassets.build.json.cache |    1 +
 .../Debug/net10.0/staticwebassets.development.json |    1 +
 .../obj/Debug/net10.0/swae.build.ex.cache          |    0
 ....NETCoreApp,Version=v10.0.AssemblyAttributes.cs |    4 +
 .../obj/Release/net10.0/Rochas.A.02DCA53C.Up2Date  |    0
 .../net10.0/Rochas.AI.Manager.Web.AssemblyInfo.cs  |   22 +
 .../Rochas.AI.Manager.Web.AssemblyInfoInputs.cache |    1 +
 ...r.Web.GeneratedMSBuildEditorConfig.editorconfig |  128 +
 .../Rochas.AI.Manager.Web.GlobalUsings.g.cs        |   17 +
 ...nager.Web.MvcApplicationPartsAssemblyInfo.cache |    0
 ....Manager.Web.MvcApplicationPartsAssemblyInfo.cs |   17 +
 .../Rochas.AI.Manager.Web.RazorAssemblyInfo.cache  |    1 +
 .../Rochas.AI.Manager.Web.RazorAssemblyInfo.cs     |   17 +
 .../net10.0/Rochas.AI.Manager.Web.assets.cache     |  Bin 0 -> 102090 bytes
 ...s.AI.Manager.Web.csproj.AssemblyReference.cache |  Bin 0 -> 42528 bytes
 ...s.AI.Manager.Web.csproj.CoreCompileInputs.cache |    1 +
 ...chas.AI.Manager.Web.csproj.FileListAbsolute.txt |  351 +
 .../obj/Release/net10.0/Rochas.AI.Manager.Web.dll  |  Bin 0 -> 525824 bytes
 .../Rochas.AI.Manager.Web.genruntimeconfig.cache   |    1 +
 .../obj/Release/net10.0/Rochas.AI.Manager.Web.pdb  |  Bin 0 -> 151636 bytes
 .../net10.0/Rochas.AI.Manager.Web.sourcelink.json  |    1 +
 .../obj/Release/net10.0/apphost                    |  Bin 0 -> 78192 bytes
 .../16lphm3rhe-{0}-fafqr8z9s7-fafqr8z9s7.gz        |  Bin 0 -> 4140 bytes
 .../25fvasn163-{0}-lfsxvok1dn-lfsxvok1dn.gz        |  Bin 0 -> 16906 bytes
 .../2eh83wy9kb-{0}-wjhuarmh55-wjhuarmh55.gz        |  Bin 0 -> 22536 bytes
 .../360rvt29bx-{0}-oheyjv9vpw-oheyjv9vpw.gz        |  Bin 0 -> 572 bytes
 .../3ixoay1p69-{0}-biywkp7jh7-biywkp7jh7.gz        |  Bin 0 -> 46077 bytes
 .../8c1d0422am-{0}-t5t3pc0fe7-t5t3pc0fe7.gz        |  Bin 0 -> 615 bytes
 .../8zijuhpgoa-{0}-1mm2lhid9t-1mm2lhid9t.gz        |  Bin 0 -> 3968 bytes
 .../a0y2led1js-{0}-nhm0lce0yj-nhm0lce0yj.gz        |  Bin 0 -> 9792 bytes
 .../bczd72z3ee-{0}-cc2kkbutuo-cc2kkbutuo.gz        |  Bin 0 -> 1867 bytes
 .../ch74w8afyx-{0}-pop50kg9ac-pop50kg9ac.gz        |  Bin 0 -> 44941 bytes
 .../g7nhtd75vp-{0}-9p7e1q840y-9p7e1q840y.gz        |  Bin 0 -> 1296 bytes
 .../jeoy3jp7at-{0}-urb3ug61ft-urb3ug61ft.gz        |  Bin 0 -> 368 bytes
 .../l5ezfu96tp-{0}-5014gehn0y-5014gehn0y.gz        |  Bin 0 -> 2508 bytes
 .../mnoywah08s-{0}-miaj7kt92n-miaj7kt92n.gz        |  Bin 0 -> 576 bytes
 .../p3c6yolfvw-{0}-aynrw99unf-aynrw99unf.gz        |  Bin 0 -> 1886 bytes
 .../q3hmrgwz0w-{0}-ulujjfsrlu-ulujjfsrlu.gz        |  Bin 0 -> 20486 bytes
 .../qemdghldm1-{0}-y091c9ksd4-y091c9ksd4.gz        |  Bin 0 -> 3294 bytes
 .../qpnx9z64kh-{0}-z7fubto1m4-z7fubto1m4.gz        |  Bin 0 -> 8005 bytes
 .../rqqsvppg5c-{0}-9pemnltd66-9pemnltd66.gz        |  Bin 0 -> 8691 bytes
 .../vy1j1k3bei-{0}-34naviyjz7-34naviyjz7.gz        |  Bin 0 -> 41540 bytes
 .../wpz2aeb0zz-{0}-860w3lioxn-860w3lioxn.gz        |  Bin 0 -> 2535 bytes
 .../wu2nf6c6fy-{0}-17btfpzddq-17btfpzddq.gz        |  Bin 0 -> 107402 bytes
 .../xdd2z0sun9-{0}-4dtgqqhnb2-4dtgqqhnb2.gz        |  Bin 0 -> 636 bytes
 .../yaelx30t1d-{0}-wc1i5xp98b-wc1i5xp98b.gz        |  Bin 0 -> 10073 bytes
 .../ym1gaa9l26-{0}-oz2w2imawu-oz2w2imawu.gz        |  Bin 0 -> 3348 bytes
 .../obj/Release/net10.0/rbcswa.dswa.cache.json     |    1 +
 .../Release/net10.0/ref/Rochas.AI.Manager.Web.dll  |  Bin 0 -> 72192 bytes
 .../net10.0/refint/Rochas.AI.Manager.Web.dll       |  Bin 0 -> 72192 bytes
 .../obj/Release/net10.0/rjimswa.dswa.cache.json    |    1 +
 .../obj/Release/net10.0/rjsmcshtml.dswa.cache.json |    1 +
 .../obj/Release/net10.0/rjsmrazor.dswa.cache.json  |    1 +
 .../obj/Release/net10.0/rpswa.dswa.cache.json      |    1 +
 .../net10.0/staticwebassets.build.endpoints.json   |    1 +
 .../obj/Release/net10.0/staticwebassets.build.json |    1 +
 .../net10.0/staticwebassets.build.json.cache       |    1 +
 .../net10.0/staticwebassets.development.json       |    1 +
 .../obj/Release/net10.0/swae.build.ex.cache        |    0
 .../Rochas.AI.Manager.Web.csproj.nuget.dgspec.json | 2267 +++++
 .../obj/Rochas.AI.Manager.Web.csproj.nuget.g.props |   26 +
 .../Rochas.AI.Manager.Web.csproj.nuget.g.targets   |   12 +
 src/Rochas.AI.Manager.Web/obj/project.assets.json  | 6575 +++++++++++++
 src/Rochas.AI.Manager.Web/obj/project.nuget.cache  |  232 +
 src/Rochas.AI.Manager.Web/wwwroot/css/black.css    |  305 +
 .../wwwroot/lib/codemirror/codemirror.css          |  344 +
 .../wwwroot/lib/codemirror/codemirror.js           | 9884 ++++++++++++++++++++
 .../wwwroot/lib/codemirror/mode/clike.js           |  942 ++
 .../wwwroot/lib/codemirror/mode/css.js             |  862 ++
 .../wwwroot/lib/codemirror/mode/htmlmixed.js       |  153 +
 .../wwwroot/lib/codemirror/mode/javascript.js      |  960 ++
 .../wwwroot/lib/codemirror/mode/markdown.js        |  886 ++
 .../wwwroot/lib/codemirror/mode/meta.js            |  221 +
 .../wwwroot/lib/codemirror/mode/python.js          |  402 +
 .../wwwroot/lib/codemirror/mode/shell.js           |  168 +
 .../wwwroot/lib/codemirror/mode/sql.js             |  525 ++
 .../wwwroot/lib/codemirror/mode/xml.js             |  417 +
 .../wwwroot/lib/codemirror/mode/yaml.js            |  120 +
 .../wwwroot/lib/codemirror/theme/dracula.css       |   40 +
 .../wwwroot/lib/codemirror/theme/eclipse.css       |   23 +
 .../lib/codemirror/theme/material-darker.css       |  135 +
 .../wwwroot/lib/codemirror/theme/monokai.css       |   41 +
 .../wwwroot/lib/diff2html/diff2html.min.css        |    1 +
 .../wwwroot/lib/diff2html/diff2html.min.js         |    1 +
 .../wwwroot/lib/diff2html/highlight-dark.min.css   |   10 +
 .../wwwroot/lib/diff2html/highlight.min.js         | 1213 +++
 .../wwwroot/lib/fontawesome/css/all.min.css        |    9 +
 .../lib/fontawesome/webfonts/fa-brands-400.woff2   |  Bin 0 -> 117852 bytes
 .../lib/fontawesome/webfonts/fa-regular-400.woff2  |  Bin 0 -> 25392 bytes
 .../lib/fontawesome/webfonts/fa-solid-900.ttf      |  Bin 0 -> 420332 bytes
 .../lib/fontawesome/webfonts/fa-solid-900.woff2    |  Bin 0 -> 156400 bytes
 .../wwwroot/lib/mdb/mdb.min.css                    |   40 +
 .../wwwroot/lib/mdb/mdb.min.js                     |   20 +
 .../AccuracyEvaluatorExtraTests.cs                 |   42 +
 .../AccuracyEvaluatorTests.cs                      |   47 +
 .../Rochas.AI.Manager.Tests/EntityHashCodeTests.cs |   30 +
 tests/Rochas.AI.Manager.Tests/ExtraServiceTests.cs |  235 +
 .../HarnessProfileServiceTests.cs                  |   58 +
 tests/Rochas.AI.Manager.Tests/HelpersTests.cs      |   82 +
 tests/Rochas.AI.Manager.Tests/ReportFilterTests.cs |  194 +
 .../ReportServiceIntegrationTests.cs               |   86 +
 .../Rochas.AI.Manager.Tests.csproj                 |   32 +
 .../SequentialCollection.cs                        |    4 +
 .../ServiceIntegrationTests.cs                     |  182 +
 .../Rochas.AI.Manager.Tests/TrainingEngineTests.cs |   43 +
 .../TrainingWorkflowServiceTests.cs                |   99 +
 tests/Rochas.AI.Manager.Tests/UnitTest1.cs         |   10 +
 ...erageSourceRootsMapping_Rochas.AI.Manager.Tests |  Bin 0 -> 3642 bytes
 .../bin/Debug/net10.0/ClosedXML.dll                |  Bin 0 -> 1438208 bytes
 ...erletSourceRootsMapping_Rochas.AI.Manager.Tests |  Bin 0 -> 3642 bytes
 .../bin/Debug/net10.0/Dapper.dll                   |  Bin 0 -> 251392 bytes
 .../bin/Debug/net10.0/DocumentFormat.OpenXml.dll   |  Bin 0 -> 6025128 bytes
 .../bin/Debug/net10.0/DotLiquid.dll                |  Bin 0 -> 110080 bytes
 .../bin/Debug/net10.0/ExcelDataReader.DataSet.dll  |  Bin 0 -> 14848 bytes
 .../bin/Debug/net10.0/ExcelDataReader.dll          |  Bin 0 -> 209408 bytes
 .../bin/Debug/net10.0/ExcelNumberFormat.dll        |  Bin 0 -> 30720 bytes
 .../bin/Debug/net10.0/Google.Protobuf.dll          |  Bin 0 -> 409368 bytes
 .../bin/Debug/net10.0/HtmlAgilityPack.dll          |  Bin 0 -> 163328 bytes
 .../bin/Debug/net10.0/ICSharpCode.SharpZipLib.dll  |  Bin 0 -> 246272 bytes
 .../bin/Debug/net10.0/Irony.dll                    |  Bin 0 -> 144896 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Black.ttf      |  Bin 0 -> 664748 bytes
 .../Debug/net10.0/LatoFont/Lato-BlackItalic.ttf    |  Bin 0 -> 698264 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Bold.ttf       |  Bin 0 -> 656544 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-BoldItalic.ttf |  Bin 0 -> 698364 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-ExtraBold.ttf  |  Bin 0 -> 613920 bytes
 .../net10.0/LatoFont/Lato-ExtraBoldItalic.ttf      |  Bin 0 -> 671876 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-ExtraLight.ttf |  Bin 0 -> 626624 bytes
 .../net10.0/LatoFont/Lato-ExtraLightItalic.ttf     |  Bin 0 -> 653640 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Italic.ttf     |  Bin 0 -> 722900 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Light.ttf      |  Bin 0 -> 643884 bytes
 .../Debug/net10.0/LatoFont/Lato-LightItalic.ttf    |  Bin 0 -> 657552 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Medium.ttf     |  Bin 0 -> 636396 bytes
 .../Debug/net10.0/LatoFont/Lato-MediumItalic.ttf   |  Bin 0 -> 694924 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Regular.ttf    |  Bin 0 -> 656568 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-SemiBold.ttf   |  Bin 0 -> 668548 bytes
 .../Debug/net10.0/LatoFont/Lato-SemiBoldItalic.ttf |  Bin 0 -> 697364 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-Thin.ttf       |  Bin 0 -> 584056 bytes
 .../bin/Debug/net10.0/LatoFont/Lato-ThinItalic.ttf |  Bin 0 -> 608716 bytes
 .../bin/Debug/net10.0/LatoFont/OFL.txt             |   93 +
 .../Debug/net10.0/Microsoft.Bcl.Cryptography.dll   |  Bin 0 -> 16168 bytes
 ...soft.Data.SqlClient.Extensions.Abstractions.dll |  Bin 0 -> 23448 bytes
 .../Microsoft.Data.SqlClient.Internal.Logging.dll  |  Bin 0 -> 53656 bytes
 .../bin/Debug/net10.0/Microsoft.Data.SqlClient.dll |  Bin 0 -> 93120 bytes
 .../bin/Debug/net10.0/Microsoft.Data.Sqlite.dll    |  Bin 0 -> 177464 bytes
 .../Microsoft.Extensions.Caching.Abstractions.dll  |  Bin 0 -> 36136 bytes
 .../Microsoft.Extensions.Caching.Memory.dll        |  Bin 0 -> 48424 bytes
 ...Extensions.DependencyInjection.Abstractions.dll |  Bin 0 -> 66312 bytes
 .../Microsoft.Extensions.DependencyInjection.dll   |  Bin 0 -> 96008 bytes
 .../Microsoft.Extensions.Logging.Abstractions.dll  |  Bin 0 -> 67344 bytes
 .../Debug/net10.0/Microsoft.Extensions.Options.dll |  Bin 0 -> 65832 bytes
 .../net10.0/Microsoft.Extensions.Primitives.dll    |  Bin 0 -> 44840 bytes
 .../Microsoft.IdentityModel.Abstractions.dll       |  Bin 0 -> 19528 bytes
 .../Microsoft.IdentityModel.JsonWebTokens.dll      |  Bin 0 -> 173088 bytes
 .../net10.0/Microsoft.IdentityModel.Logging.dll    |  Bin 0 -> 38432 bytes
 ...osoft.IdentityModel.Protocols.OpenIdConnect.dll |  Bin 0 -> 137248 bytes
 .../net10.0/Microsoft.IdentityModel.Protocols.dll  |  Bin 0 -> 53280 bytes
 .../net10.0/Microsoft.IdentityModel.Tokens.dll     |  Bin 0 -> 391200 bytes
 .../Debug/net10.0/Microsoft.SqlServer.Server.dll   |  Bin 0 -> 23976 bytes
 ...crosoft.TestPlatform.CommunicationUtilities.dll |  Bin 0 -> 137776 bytes
 .../Microsoft.TestPlatform.CoreUtilities.dll       |  Bin 0 -> 98336 bytes
 .../Microsoft.TestPlatform.CrossPlatEngine.dll     |  Bin 0 -> 316960 bytes
 ...Microsoft.TestPlatform.PlatformAbstractions.dll |  Bin 0 -> 51232 bytes
 .../net10.0/Microsoft.TestPlatform.Utilities.dll   |  Bin 0 -> 67104 bytes
 .../Microsoft.VisualStudio.CodeCoverage.Shim.dll   |  Bin 0 -> 17464 bytes
 .../Microsoft.VisualStudio.TestPlatform.Common.dll |  Bin 0 -> 248352 bytes
 ...osoft.VisualStudio.TestPlatform.ObjectModel.dll |  Bin 0 -> 334392 bytes
 .../bin/Debug/net10.0/MySqlConnector.dll           |  Bin 0 -> 880488 bytes
 .../net10.0/NJsonSchema.CodeGeneration.CSharp.dll  |  Bin 0 -> 46592 bytes
 .../Debug/net10.0/NJsonSchema.CodeGeneration.dll   |  Bin 0 -> 32768 bytes
 .../bin/Debug/net10.0/NJsonSchema.dll              |  Bin 0 -> 222208 bytes
 .../bin/Debug/net10.0/Namotion.Reflection.dll      |  Bin 0 -> 47104 bytes
 .../bin/Debug/net10.0/Newtonsoft.Json.dll          |  Bin 0 -> 712464 bytes
 .../bin/Debug/net10.0/Npgsql.dll                   |  Bin 0 -> 1409024 bytes
 .../bin/Debug/net10.0/OpenGraphNet.dll             |  Bin 0 -> 31232 bytes
 .../bin/Debug/net10.0/QRCoder.dll                  |  Bin 0 -> 131584 bytes
 .../bin/Debug/net10.0/QuestPDF.dll                 |  Bin 0 -> 650752 bytes
 .../net10.0/Rochas.AI.Manager.Application.dll      |  Bin 0 -> 165376 bytes
 .../net10.0/Rochas.AI.Manager.Application.pdb      |  Bin 0 -> 52736 bytes
 .../bin/Debug/net10.0/Rochas.AI.Manager.Domain.dll |  Bin 0 -> 53248 bytes
 .../bin/Debug/net10.0/Rochas.AI.Manager.Domain.pdb |  Bin 0 -> 27328 bytes
 .../net10.0/Rochas.AI.Manager.Infrastructure.dll   |  Bin 0 -> 56832 bytes
 .../net10.0/Rochas.AI.Manager.Infrastructure.pdb   |  Bin 0 -> 21384 bytes
 .../net10.0/Rochas.AI.Manager.Tests.deps.json      | 1994 ++++
 .../bin/Debug/net10.0/Rochas.AI.Manager.Tests.dll  |  Bin 0 -> 90624 bytes
 .../bin/Debug/net10.0/Rochas.AI.Manager.Tests.pdb  |  Bin 0 -> 35716 bytes
 .../Rochas.AI.Manager.Tests.runtimeconfig.json     |   13 +
 .../net10.0/Rochas.AI.Manager.TrainingEngine.dll   |  Bin 0 -> 27136 bytes
 .../net10.0/Rochas.AI.Manager.TrainingEngine.pdb   |  Bin 0 -> 20436 bytes
 .../bin/Debug/net10.0/Rochas.BWOQ.dll              |  Bin 0 -> 57344 bytes
 .../bin/Debug/net10.0/Rochas.DapperRepository.dll  |  Bin 0 -> 71168 bytes
 .../Debug/net10.0/Rochas.Data.Specification.dll    |  Bin 0 -> 13824 bytes
 .../bin/Debug/net10.0/Rochas.ExcelToJsonParser.dll |  Bin 0 -> 36352 bytes
 .../bin/Debug/net10.0/Rochas.ImageGenerator.dll    |  Bin 0 -> 18944 bytes
 .../bin/Debug/net10.0/Rochas.PDFGenerator.dll      |  Bin 0 -> 3032576 bytes
 .../bin/Debug/net10.0/Rochas.SqlWrapper.dll        |  Bin 0 -> 46080 bytes
 .../Debug/net10.0/SQLitePCLRaw.batteries_v2.dll    |  Bin 0 -> 5120 bytes
 .../bin/Debug/net10.0/SQLitePCLRaw.core.dll        |  Bin 0 -> 51200 bytes
 .../net10.0/SQLitePCLRaw.provider.e_sqlite3.dll    |  Bin 0 -> 36864 bytes
 .../bin/Debug/net10.0/SixLabors.Fonts.dll          |  Bin 0 -> 1135616 bytes
 .../Debug/net10.0/SixLabors.ImageSharp.Drawing.dll |  Bin 0 -> 173056 bytes
 .../bin/Debug/net10.0/SixLabors.ImageSharp.dll     |  Bin 0 -> 1806848 bytes
 .../bin/Debug/net10.0/SkiaSharp.dll                |  Bin 0 -> 432568 bytes
 .../System.Configuration.ConfigurationManager.dll  |  Bin 0 -> 442664 bytes
 .../Debug/net10.0/System.Diagnostics.EventLog.dll  |  Bin 0 -> 53072 bytes
 .../bin/Debug/net10.0/System.IO.Packaging.dll      |  Bin 0 -> 122480 bytes
 .../net10.0/System.IdentityModel.Tokens.Jwt.dll    |  Bin 0 -> 92232 bytes
 .../bin/Debug/net10.0/System.Linq.Dynamic.Core.dll |  Bin 0 -> 247296 bytes
 .../net10.0/System.Security.Cryptography.Pkcs.dll  |  Bin 0 -> 265000 bytes
 .../System.Security.Cryptography.ProtectedData.dll |  Bin 0 -> 38184 bytes
 .../net10.0/System.Threading.RateLimiting.dll      |  Bin 0 -> 69416 bytes
 .../bin/Debug/net10.0/TorchSharp.dll               |  Bin 0 -> 1556920 bytes
 .../bin/Debug/net10.0/XLParser.dll                 |  Bin 0 -> 54784 bytes
 .../cs/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 139672 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16440 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 16944 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 21040 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 20520 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 23624 bytes
 .../de/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 144792 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16928 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 16952 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 21048 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 21064 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 24136 bytes
 .../es/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 141720 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16928 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 16952 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 21032 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 21040 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 24120 bytes
 .../fr/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 147352 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16952 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 16944 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 21040 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 21048 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 24096 bytes
 .../bin/Debug/net10.0/it/DotLiquid.resources.dll   |  Bin 0 -> 9728 bytes
 .../it/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 142272 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16928 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 16928 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 21048 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 21040 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 24096 bytes
 .../ja/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 156568 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16928 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 17440 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 22088 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 21552 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 24632 bytes
 .../ko/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 144280 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16944 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 16936 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 21560 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 21048 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 24120 bytes
 .../pl/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 144792 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16928 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 16936 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 21536 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 21048 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 24136 bytes
 .../pt-BR/Microsoft.Data.SqlClient.resources.dll   |  Bin 0 -> 138688 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16416 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 16928 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 21048 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 21048 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 24120 bytes
 .../ru/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 186776 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 17440 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 17464 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 23072 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 23096 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 26168 bytes
 .../browser-wasm/nativeassets/net9.0/e_sqlite3.a   |  Bin 0 -> 1308854 bytes
 .../runtimes/linux-arm/native/libe_sqlite3.so      |  Bin 0 -> 982224 bytes
 .../runtimes/linux-arm64/native/libQuestPdfSkia.so |  Bin 0 -> 7542864 bytes
 .../runtimes/linux-arm64/native/libe_sqlite3.so    |  Bin 0 -> 1534296 bytes
 .../net10.0/runtimes/linux-arm64/native/libqpdf.so |  Bin 0 -> 3028440 bytes
 .../runtimes/linux-armel/native/libe_sqlite3.so    |  Bin 0 -> 1375344 bytes
 .../runtimes/linux-mips64/native/libe_sqlite3.so   |  Bin 0 -> 1756544 bytes
 .../runtimes/linux-musl-arm/native/libe_sqlite3.so |  Bin 0 -> 1322584 bytes
 .../linux-musl-arm64/native/libe_sqlite3.so        |  Bin 0 -> 1450704 bytes
 .../linux-musl-riscv64/native/libe_sqlite3.so      |  Bin 0 -> 1015888 bytes
 .../linux-musl-s390x/native/libe_sqlite3.so        |  Bin 0 -> 1685752 bytes
 .../linux-musl-x64/native/libQuestPdfSkia.so       |  Bin 0 -> 8338648 bytes
 .../runtimes/linux-musl-x64/native/libe_sqlite3.so |  Bin 0 -> 1445336 bytes
 .../runtimes/linux-musl-x64/native/libqpdf.so      |  Bin 0 -> 3297984 bytes
 .../runtimes/linux-ppc64le/native/libe_sqlite3.so  |  Bin 0 -> 1937408 bytes
 .../runtimes/linux-riscv64/native/libe_sqlite3.so  |  Bin 0 -> 1291896 bytes
 .../runtimes/linux-s390x/native/libe_sqlite3.so    |  Bin 0 -> 1655776 bytes
 .../runtimes/linux-x64/native/libLibTorchSharp.so  |  Bin 0 -> 2399008 bytes
 .../runtimes/linux-x64/native/libQuestPdfSkia.so   |  Bin 0 -> 8104976 bytes
 .../runtimes/linux-x64/native/libe_sqlite3.so      |  Bin 0 -> 1470784 bytes
 .../net10.0/runtimes/linux-x64/native/libqpdf.so   |  Bin 0 -> 3249656 bytes
 .../runtimes/linux-x86/native/libe_sqlite3.so      |  Bin 0 -> 1547984 bytes
 .../maccatalyst-arm64/native/libe_sqlite3.dylib    |  Bin 0 -> 1695264 bytes
 .../maccatalyst-x64/native/libe_sqlite3.dylib      |  Bin 0 -> 1724352 bytes
 .../osx-arm64/native/libLibTorchSharp.dylib        |  Bin 0 -> 2139480 bytes
 .../osx-arm64/native/libQuestPdfSkia.dylib         |  Bin 0 -> 6661112 bytes
 .../runtimes/osx-arm64/native/libe_sqlite3.dylib   |  Bin 0 -> 1696624 bytes
 .../runtimes/osx-arm64/native/libqpdf.dylib        |  Bin 0 -> 2429808 bytes
 .../runtimes/osx-x64/native/libQuestPdfSkia.dylib  |  Bin 0 -> 7289488 bytes
 .../runtimes/osx-x64/native/libe_sqlite3.dylib     |  Bin 0 -> 1724472 bytes
 .../net10.0/runtimes/osx-x64/native/libqpdf.dylib  |  Bin 0 -> 2457208 bytes
 .../net10.0/runtimes/osx/native/libSkiaSharp.dylib |  Bin 0 -> 15563136 bytes
 .../unix/lib/net9.0/Microsoft.Data.SqlClient.dll   |  Bin 0 -> 1670040 bytes
 .../runtimes/win-arm64/native/LibTorchSharp.dll    |  Bin 0 -> 1848760 bytes
 .../native/Microsoft.Data.SqlClient.SNI.dll        |  Bin 0 -> 537496 bytes
 .../runtimes/win-arm64/native/QuestPdfSkia.dll     |  Bin 0 -> 6909440 bytes
 .../runtimes/win-arm64/native/e_sqlite3.dll        |  Bin 0 -> 1814528 bytes
 .../runtimes/win-arm64/native/libSkiaSharp.dll     |  Bin 0 -> 8084920 bytes
 .../net10.0/runtimes/win-arm64/native/qpdf.dll     |  Bin 0 -> 2868736 bytes
 .../runtimes/win-x64/native/LibTorchSharp.dll      |  Bin 0 -> 1324984 bytes
 .../native/Microsoft.Data.SqlClient.SNI.dll        |  Bin 0 -> 555416 bytes
 .../runtimes/win-x64/native/QuestPdfSkia.dll       |  Bin 0 -> 8161280 bytes
 .../net10.0/runtimes/win-x64/native/e_sqlite3.dll  |  Bin 0 -> 1978880 bytes
 .../runtimes/win-x64/native/libSkiaSharp.dll       |  Bin 0 -> 9580984 bytes
 .../Debug/net10.0/runtimes/win-x64/native/qpdf.dll |  Bin 0 -> 4396544 bytes
 .../native/Microsoft.Data.SqlClient.SNI.dll        |  Bin 0 -> 468880 bytes
 .../runtimes/win-x86/native/QuestPdfSkia.dll       |  Bin 0 -> 7379968 bytes
 .../net10.0/runtimes/win-x86/native/e_sqlite3.dll  |  Bin 0 -> 1533952 bytes
 .../runtimes/win-x86/native/libSkiaSharp.dll       |  Bin 0 -> 8139200 bytes
 .../Debug/net10.0/runtimes/win-x86/native/qpdf.dll |  Bin 0 -> 4743680 bytes
 .../win/lib/net9.0/Microsoft.Data.SqlClient.dll    |  Bin 0 -> 1670040 bytes
 .../System.Diagnostics.EventLog.Messages.dll       |  Bin 0 -> 800552 bytes
 .../win/lib/net9.0/System.Diagnostics.EventLog.dll |  Bin 0 -> 174416 bytes
 .../net9.0/System.Security.Cryptography.Pkcs.dll   |  Bin 0 -> 318800 bytes
 .../bin/Debug/net10.0/testhost.dll                 |  Bin 0 -> 49696 bytes
 .../tr/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 135576 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16952 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 16928 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 21024 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 20536 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 23608 bytes
 .../bin/Debug/net10.0/xunit.abstractions.dll       |  Bin 0 -> 21928 bytes
 .../bin/Debug/net10.0/xunit.assert.dll             |  Bin 0 -> 187088 bytes
 .../bin/Debug/net10.0/xunit.core.dll               |  Bin 0 -> 70352 bytes
 .../bin/Debug/net10.0/xunit.execution.dotnet.dll   |  Bin 0 -> 279760 bytes
 .../xunit.runner.visualstudio.testadapter.dll      |  Bin 0 -> 1251840 bytes
 .../zh-Hans/Microsoft.Data.SqlClient.resources.dll |  Bin 0 -> 122264 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16416 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 16928 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 20008 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 20040 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 23072 bytes
 .../zh-Hant/Microsoft.Data.SqlClient.resources.dll |  Bin 0 -> 123840 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16416 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 16952 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 20552 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 20024 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 23096 bytes
 ...erageSourceRootsMapping_Rochas.AI.Manager.Tests |  Bin 0 -> 3642 bytes
 .../bin/Release/net10.0/ClosedXML.dll              |  Bin 0 -> 1438208 bytes
 ...erletSourceRootsMapping_Rochas.AI.Manager.Tests |  Bin 0 -> 3642 bytes
 .../bin/Release/net10.0/Dapper.dll                 |  Bin 0 -> 251392 bytes
 .../bin/Release/net10.0/DocumentFormat.OpenXml.dll |  Bin 0 -> 6025128 bytes
 .../bin/Release/net10.0/DotLiquid.dll              |  Bin 0 -> 110080 bytes
 .../Release/net10.0/ExcelDataReader.DataSet.dll    |  Bin 0 -> 14848 bytes
 .../bin/Release/net10.0/ExcelDataReader.dll        |  Bin 0 -> 209408 bytes
 .../bin/Release/net10.0/ExcelNumberFormat.dll      |  Bin 0 -> 30720 bytes
 .../bin/Release/net10.0/Google.Protobuf.dll        |  Bin 0 -> 409368 bytes
 .../bin/Release/net10.0/HtmlAgilityPack.dll        |  Bin 0 -> 163328 bytes
 .../Release/net10.0/ICSharpCode.SharpZipLib.dll    |  Bin 0 -> 246272 bytes
 .../bin/Release/net10.0/Irony.dll                  |  Bin 0 -> 144896 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Black.ttf    |  Bin 0 -> 664748 bytes
 .../Release/net10.0/LatoFont/Lato-BlackItalic.ttf  |  Bin 0 -> 698264 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Bold.ttf     |  Bin 0 -> 656544 bytes
 .../Release/net10.0/LatoFont/Lato-BoldItalic.ttf   |  Bin 0 -> 698364 bytes
 .../Release/net10.0/LatoFont/Lato-ExtraBold.ttf    |  Bin 0 -> 613920 bytes
 .../net10.0/LatoFont/Lato-ExtraBoldItalic.ttf      |  Bin 0 -> 671876 bytes
 .../Release/net10.0/LatoFont/Lato-ExtraLight.ttf   |  Bin 0 -> 626624 bytes
 .../net10.0/LatoFont/Lato-ExtraLightItalic.ttf     |  Bin 0 -> 653640 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Italic.ttf   |  Bin 0 -> 722900 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Light.ttf    |  Bin 0 -> 643884 bytes
 .../Release/net10.0/LatoFont/Lato-LightItalic.ttf  |  Bin 0 -> 657552 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Medium.ttf   |  Bin 0 -> 636396 bytes
 .../Release/net10.0/LatoFont/Lato-MediumItalic.ttf |  Bin 0 -> 694924 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Regular.ttf  |  Bin 0 -> 656568 bytes
 .../bin/Release/net10.0/LatoFont/Lato-SemiBold.ttf |  Bin 0 -> 668548 bytes
 .../net10.0/LatoFont/Lato-SemiBoldItalic.ttf       |  Bin 0 -> 697364 bytes
 .../bin/Release/net10.0/LatoFont/Lato-Thin.ttf     |  Bin 0 -> 584056 bytes
 .../Release/net10.0/LatoFont/Lato-ThinItalic.ttf   |  Bin 0 -> 608716 bytes
 .../bin/Release/net10.0/LatoFont/OFL.txt           |   93 +
 .../Release/net10.0/Microsoft.Bcl.Cryptography.dll |  Bin 0 -> 16168 bytes
 ...soft.Data.SqlClient.Extensions.Abstractions.dll |  Bin 0 -> 23448 bytes
 .../Microsoft.Data.SqlClient.Internal.Logging.dll  |  Bin 0 -> 53656 bytes
 .../Release/net10.0/Microsoft.Data.SqlClient.dll   |  Bin 0 -> 93120 bytes
 .../bin/Release/net10.0/Microsoft.Data.Sqlite.dll  |  Bin 0 -> 177464 bytes
 .../Microsoft.Extensions.Caching.Abstractions.dll  |  Bin 0 -> 36136 bytes
 .../Microsoft.Extensions.Caching.Memory.dll        |  Bin 0 -> 48424 bytes
 ...Extensions.DependencyInjection.Abstractions.dll |  Bin 0 -> 66312 bytes
 .../Microsoft.Extensions.DependencyInjection.dll   |  Bin 0 -> 96008 bytes
 .../Microsoft.Extensions.Logging.Abstractions.dll  |  Bin 0 -> 67344 bytes
 .../net10.0/Microsoft.Extensions.Options.dll       |  Bin 0 -> 65832 bytes
 .../net10.0/Microsoft.Extensions.Primitives.dll    |  Bin 0 -> 44840 bytes
 .../Microsoft.IdentityModel.Abstractions.dll       |  Bin 0 -> 19528 bytes
 .../Microsoft.IdentityModel.JsonWebTokens.dll      |  Bin 0 -> 173088 bytes
 .../net10.0/Microsoft.IdentityModel.Logging.dll    |  Bin 0 -> 38432 bytes
 ...osoft.IdentityModel.Protocols.OpenIdConnect.dll |  Bin 0 -> 137248 bytes
 .../net10.0/Microsoft.IdentityModel.Protocols.dll  |  Bin 0 -> 53280 bytes
 .../net10.0/Microsoft.IdentityModel.Tokens.dll     |  Bin 0 -> 391200 bytes
 .../Release/net10.0/Microsoft.SqlServer.Server.dll |  Bin 0 -> 23976 bytes
 ...crosoft.TestPlatform.CommunicationUtilities.dll |  Bin 0 -> 137776 bytes
 .../Microsoft.TestPlatform.CoreUtilities.dll       |  Bin 0 -> 98336 bytes
 .../Microsoft.TestPlatform.CrossPlatEngine.dll     |  Bin 0 -> 316960 bytes
 ...Microsoft.TestPlatform.PlatformAbstractions.dll |  Bin 0 -> 51232 bytes
 .../net10.0/Microsoft.TestPlatform.Utilities.dll   |  Bin 0 -> 67104 bytes
 .../Microsoft.VisualStudio.CodeCoverage.Shim.dll   |  Bin 0 -> 17464 bytes
 .../Microsoft.VisualStudio.TestPlatform.Common.dll |  Bin 0 -> 248352 bytes
 ...osoft.VisualStudio.TestPlatform.ObjectModel.dll |  Bin 0 -> 334392 bytes
 .../bin/Release/net10.0/MySqlConnector.dll         |  Bin 0 -> 880488 bytes
 .../net10.0/NJsonSchema.CodeGeneration.CSharp.dll  |  Bin 0 -> 46592 bytes
 .../Release/net10.0/NJsonSchema.CodeGeneration.dll |  Bin 0 -> 32768 bytes
 .../bin/Release/net10.0/NJsonSchema.dll            |  Bin 0 -> 222208 bytes
 .../bin/Release/net10.0/Namotion.Reflection.dll    |  Bin 0 -> 47104 bytes
 .../bin/Release/net10.0/Newtonsoft.Json.dll        |  Bin 0 -> 712464 bytes
 .../bin/Release/net10.0/Npgsql.dll                 |  Bin 0 -> 1409024 bytes
 .../bin/Release/net10.0/OpenGraphNet.dll           |  Bin 0 -> 31232 bytes
 .../bin/Release/net10.0/QRCoder.dll                |  Bin 0 -> 131584 bytes
 .../bin/Release/net10.0/QuestPDF.dll               |  Bin 0 -> 650752 bytes
 .../net10.0/Rochas.AI.Manager.Application.dll      |  Bin 0 -> 148992 bytes
 .../net10.0/Rochas.AI.Manager.Application.pdb      |  Bin 0 -> 44356 bytes
 .../Release/net10.0/Rochas.AI.Manager.Domain.dll   |  Bin 0 -> 52224 bytes
 .../Release/net10.0/Rochas.AI.Manager.Domain.pdb   |  Bin 0 -> 27016 bytes
 .../net10.0/Rochas.AI.Manager.Infrastructure.dll   |  Bin 0 -> 51200 bytes
 .../net10.0/Rochas.AI.Manager.Infrastructure.pdb   |  Bin 0 -> 20144 bytes
 .../net10.0/Rochas.AI.Manager.Tests.deps.json      | 1994 ++++
 .../Release/net10.0/Rochas.AI.Manager.Tests.dll    |  Bin 0 -> 79872 bytes
 .../Release/net10.0/Rochas.AI.Manager.Tests.pdb    |  Bin 0 -> 32880 bytes
 .../Rochas.AI.Manager.Tests.runtimeconfig.json     |   14 +
 .../net10.0/Rochas.AI.Manager.TrainingEngine.dll   |  Bin 0 -> 23040 bytes
 .../net10.0/Rochas.AI.Manager.TrainingEngine.pdb   |  Bin 0 -> 19144 bytes
 .../bin/Release/net10.0/Rochas.BWOQ.dll            |  Bin 0 -> 57344 bytes
 .../Release/net10.0/Rochas.DapperRepository.dll    |  Bin 0 -> 71168 bytes
 .../Release/net10.0/Rochas.Data.Specification.dll  |  Bin 0 -> 13824 bytes
 .../Release/net10.0/Rochas.ExcelToJsonParser.dll   |  Bin 0 -> 36352 bytes
 .../bin/Release/net10.0/Rochas.ImageGenerator.dll  |  Bin 0 -> 18944 bytes
 .../bin/Release/net10.0/Rochas.PDFGenerator.dll    |  Bin 0 -> 3032576 bytes
 .../bin/Release/net10.0/Rochas.SqlWrapper.dll      |  Bin 0 -> 46080 bytes
 .../Release/net10.0/SQLitePCLRaw.batteries_v2.dll  |  Bin 0 -> 5120 bytes
 .../bin/Release/net10.0/SQLitePCLRaw.core.dll      |  Bin 0 -> 51200 bytes
 .../net10.0/SQLitePCLRaw.provider.e_sqlite3.dll    |  Bin 0 -> 36864 bytes
 .../bin/Release/net10.0/SixLabors.Fonts.dll        |  Bin 0 -> 1135616 bytes
 .../net10.0/SixLabors.ImageSharp.Drawing.dll       |  Bin 0 -> 173056 bytes
 .../bin/Release/net10.0/SixLabors.ImageSharp.dll   |  Bin 0 -> 1806848 bytes
 .../bin/Release/net10.0/SkiaSharp.dll              |  Bin 0 -> 432568 bytes
 .../System.Configuration.ConfigurationManager.dll  |  Bin 0 -> 442664 bytes
 .../net10.0/System.Diagnostics.EventLog.dll        |  Bin 0 -> 53072 bytes
 .../bin/Release/net10.0/System.IO.Packaging.dll    |  Bin 0 -> 122480 bytes
 .../net10.0/System.IdentityModel.Tokens.Jwt.dll    |  Bin 0 -> 92232 bytes
 .../Release/net10.0/System.Linq.Dynamic.Core.dll   |  Bin 0 -> 247296 bytes
 .../net10.0/System.Security.Cryptography.Pkcs.dll  |  Bin 0 -> 265000 bytes
 .../System.Security.Cryptography.ProtectedData.dll |  Bin 0 -> 38184 bytes
 .../net10.0/System.Threading.RateLimiting.dll      |  Bin 0 -> 69416 bytes
 .../bin/Release/net10.0/TorchSharp.dll             |  Bin 0 -> 1556920 bytes
 .../bin/Release/net10.0/XLParser.dll               |  Bin 0 -> 54784 bytes
 .../cs/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 139672 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16440 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 16944 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 21040 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 20520 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 23624 bytes
 .../de/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 144792 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16928 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 16952 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 21048 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 21064 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 24136 bytes
 .../es/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 141720 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16928 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 16952 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 21032 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 21040 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 24120 bytes
 .../fr/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 147352 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16952 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 16944 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 21040 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 21048 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 24096 bytes
 .../bin/Release/net10.0/it/DotLiquid.resources.dll |  Bin 0 -> 9728 bytes
 .../it/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 142272 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16928 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 16928 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 21048 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 21040 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 24096 bytes
 .../ja/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 156568 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16928 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 17440 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 22088 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 21552 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 24632 bytes
 .../ko/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 144280 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16944 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 16936 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 21560 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 21048 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 24120 bytes
 .../pl/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 144792 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16928 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 16936 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 21536 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 21048 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 24136 bytes
 .../pt-BR/Microsoft.Data.SqlClient.resources.dll   |  Bin 0 -> 138688 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16416 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 16928 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 21048 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 21048 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 24120 bytes
 .../ru/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 186776 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 17440 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 17464 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 23072 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 23096 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 26168 bytes
 .../browser-wasm/nativeassets/net9.0/e_sqlite3.a   |  Bin 0 -> 1308854 bytes
 .../runtimes/linux-arm/native/libe_sqlite3.so      |  Bin 0 -> 982224 bytes
 .../runtimes/linux-arm64/native/libQuestPdfSkia.so |  Bin 0 -> 7542864 bytes
 .../runtimes/linux-arm64/native/libe_sqlite3.so    |  Bin 0 -> 1534296 bytes
 .../net10.0/runtimes/linux-arm64/native/libqpdf.so |  Bin 0 -> 3028440 bytes
 .../runtimes/linux-armel/native/libe_sqlite3.so    |  Bin 0 -> 1375344 bytes
 .../runtimes/linux-mips64/native/libe_sqlite3.so   |  Bin 0 -> 1756544 bytes
 .../runtimes/linux-musl-arm/native/libe_sqlite3.so |  Bin 0 -> 1322584 bytes
 .../linux-musl-arm64/native/libe_sqlite3.so        |  Bin 0 -> 1450704 bytes
 .../linux-musl-riscv64/native/libe_sqlite3.so      |  Bin 0 -> 1015888 bytes
 .../linux-musl-s390x/native/libe_sqlite3.so        |  Bin 0 -> 1685752 bytes
 .../linux-musl-x64/native/libQuestPdfSkia.so       |  Bin 0 -> 8338648 bytes
 .../runtimes/linux-musl-x64/native/libe_sqlite3.so |  Bin 0 -> 1445336 bytes
 .../runtimes/linux-musl-x64/native/libqpdf.so      |  Bin 0 -> 3297984 bytes
 .../runtimes/linux-ppc64le/native/libe_sqlite3.so  |  Bin 0 -> 1937408 bytes
 .../runtimes/linux-riscv64/native/libe_sqlite3.so  |  Bin 0 -> 1291896 bytes
 .../runtimes/linux-s390x/native/libe_sqlite3.so    |  Bin 0 -> 1655776 bytes
 .../runtimes/linux-x64/native/libLibTorchSharp.so  |  Bin 0 -> 2399008 bytes
 .../runtimes/linux-x64/native/libQuestPdfSkia.so   |  Bin 0 -> 8104976 bytes
 .../runtimes/linux-x64/native/libe_sqlite3.so      |  Bin 0 -> 1470784 bytes
 .../net10.0/runtimes/linux-x64/native/libqpdf.so   |  Bin 0 -> 3249656 bytes
 .../runtimes/linux-x86/native/libe_sqlite3.so      |  Bin 0 -> 1547984 bytes
 .../maccatalyst-arm64/native/libe_sqlite3.dylib    |  Bin 0 -> 1695264 bytes
 .../maccatalyst-x64/native/libe_sqlite3.dylib      |  Bin 0 -> 1724352 bytes
 .../osx-arm64/native/libLibTorchSharp.dylib        |  Bin 0 -> 2139480 bytes
 .../osx-arm64/native/libQuestPdfSkia.dylib         |  Bin 0 -> 6661112 bytes
 .../runtimes/osx-arm64/native/libe_sqlite3.dylib   |  Bin 0 -> 1696624 bytes
 .../runtimes/osx-arm64/native/libqpdf.dylib        |  Bin 0 -> 2429808 bytes
 .../runtimes/osx-x64/native/libQuestPdfSkia.dylib  |  Bin 0 -> 7289488 bytes
 .../runtimes/osx-x64/native/libe_sqlite3.dylib     |  Bin 0 -> 1724472 bytes
 .../net10.0/runtimes/osx-x64/native/libqpdf.dylib  |  Bin 0 -> 2457208 bytes
 .../net10.0/runtimes/osx/native/libSkiaSharp.dylib |  Bin 0 -> 15563136 bytes
 .../unix/lib/net9.0/Microsoft.Data.SqlClient.dll   |  Bin 0 -> 1670040 bytes
 .../runtimes/win-arm64/native/LibTorchSharp.dll    |  Bin 0 -> 1848760 bytes
 .../native/Microsoft.Data.SqlClient.SNI.dll        |  Bin 0 -> 537496 bytes
 .../runtimes/win-arm64/native/QuestPdfSkia.dll     |  Bin 0 -> 6909440 bytes
 .../runtimes/win-arm64/native/e_sqlite3.dll        |  Bin 0 -> 1814528 bytes
 .../runtimes/win-arm64/native/libSkiaSharp.dll     |  Bin 0 -> 8084920 bytes
 .../net10.0/runtimes/win-arm64/native/qpdf.dll     |  Bin 0 -> 2868736 bytes
 .../runtimes/win-x64/native/LibTorchSharp.dll      |  Bin 0 -> 1324984 bytes
 .../native/Microsoft.Data.SqlClient.SNI.dll        |  Bin 0 -> 555416 bytes
 .../runtimes/win-x64/native/QuestPdfSkia.dll       |  Bin 0 -> 8161280 bytes
 .../net10.0/runtimes/win-x64/native/e_sqlite3.dll  |  Bin 0 -> 1978880 bytes
 .../runtimes/win-x64/native/libSkiaSharp.dll       |  Bin 0 -> 9580984 bytes
 .../net10.0/runtimes/win-x64/native/qpdf.dll       |  Bin 0 -> 4396544 bytes
 .../native/Microsoft.Data.SqlClient.SNI.dll        |  Bin 0 -> 468880 bytes
 .../runtimes/win-x86/native/QuestPdfSkia.dll       |  Bin 0 -> 7379968 bytes
 .../net10.0/runtimes/win-x86/native/e_sqlite3.dll  |  Bin 0 -> 1533952 bytes
 .../runtimes/win-x86/native/libSkiaSharp.dll       |  Bin 0 -> 8139200 bytes
 .../net10.0/runtimes/win-x86/native/qpdf.dll       |  Bin 0 -> 4743680 bytes
 .../win/lib/net9.0/Microsoft.Data.SqlClient.dll    |  Bin 0 -> 1670040 bytes
 .../System.Diagnostics.EventLog.Messages.dll       |  Bin 0 -> 800552 bytes
 .../win/lib/net9.0/System.Diagnostics.EventLog.dll |  Bin 0 -> 174416 bytes
 .../net9.0/System.Security.Cryptography.Pkcs.dll   |  Bin 0 -> 318800 bytes
 .../bin/Release/net10.0/testhost.dll               |  Bin 0 -> 49696 bytes
 .../tr/Microsoft.Data.SqlClient.resources.dll      |  Bin 0 -> 135576 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16952 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 16928 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 21024 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 20536 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 23608 bytes
 .../bin/Release/net10.0/xunit.abstractions.dll     |  Bin 0 -> 21928 bytes
 .../bin/Release/net10.0/xunit.assert.dll           |  Bin 0 -> 187088 bytes
 .../bin/Release/net10.0/xunit.core.dll             |  Bin 0 -> 70352 bytes
 .../bin/Release/net10.0/xunit.execution.dotnet.dll |  Bin 0 -> 279760 bytes
 .../xunit.runner.visualstudio.testadapter.dll      |  Bin 0 -> 1251840 bytes
 .../zh-Hans/Microsoft.Data.SqlClient.resources.dll |  Bin 0 -> 122264 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16416 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 16928 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 20008 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 20040 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 23072 bytes
 .../zh-Hant/Microsoft.Data.SqlClient.resources.dll |  Bin 0 -> 123840 bytes
 ...stPlatform.CommunicationUtilities.resources.dll |  Bin 0 -> 16416 bytes
 ...rosoft.TestPlatform.CoreUtilities.resources.dll |  Bin 0 -> 16952 bytes
 ...soft.TestPlatform.CrossPlatEngine.resources.dll |  Bin 0 -> 20552 bytes
 ....VisualStudio.TestPlatform.Common.resources.dll |  Bin 0 -> 20024 bytes
 ...alStudio.TestPlatform.ObjectModel.resources.dll |  Bin 0 -> 23096 bytes
 ....NETCoreApp,Version=v10.0.AssemblyAttributes.cs |    4 +
 .../obj/Debug/net10.0/Rochas.A.B61F05EA.Up2Date    |    0
 .../Rochas.AI.Manager.Tests.AssemblyInfo.cs        |   22 +
 ...ochas.AI.Manager.Tests.AssemblyInfoInputs.cache |    1 +
 ...Tests.GeneratedMSBuildEditorConfig.editorconfig |   18 +
 .../Rochas.AI.Manager.Tests.GlobalUsings.g.cs      |    9 +
 .../net10.0/Rochas.AI.Manager.Tests.assets.cache   |  Bin 0 -> 81924 bytes
 ...AI.Manager.Tests.csproj.AssemblyReference.cache |  Bin 0 -> 36859 bytes
 ...AI.Manager.Tests.csproj.CoreCompileInputs.cache |    1 +
 ...as.AI.Manager.Tests.csproj.FileListAbsolute.txt |  357 +
 .../obj/Debug/net10.0/Rochas.AI.Manager.Tests.dll  |  Bin 0 -> 90624 bytes
 .../Rochas.AI.Manager.Tests.genruntimeconfig.cache |    1 +
 .../obj/Debug/net10.0/Rochas.AI.Manager.Tests.pdb  |  Bin 0 -> 35716 bytes
 .../Rochas.AI.Manager.Tests.sourcelink.json        |    1 +
 .../Debug/net10.0/ref/Rochas.AI.Manager.Tests.dll  |  Bin 0 -> 23552 bytes
 .../net10.0/refint/Rochas.AI.Manager.Tests.dll     |  Bin 0 -> 23552 bytes
 ....NETCoreApp,Version=v10.0.AssemblyAttributes.cs |    4 +
 .../obj/Release/net10.0/Rochas.A.B61F05EA.Up2Date  |    0
 .../Rochas.AI.Manager.Tests.AssemblyInfo.cs        |   22 +
 ...ochas.AI.Manager.Tests.AssemblyInfoInputs.cache |    1 +
 ...Tests.GeneratedMSBuildEditorConfig.editorconfig |   18 +
 .../Rochas.AI.Manager.Tests.GlobalUsings.g.cs      |    9 +
 .../net10.0/Rochas.AI.Manager.Tests.assets.cache   |  Bin 0 -> 81924 bytes
 ...AI.Manager.Tests.csproj.AssemblyReference.cache |  Bin 0 -> 36867 bytes
 ...AI.Manager.Tests.csproj.CoreCompileInputs.cache |    1 +
 ...as.AI.Manager.Tests.csproj.FileListAbsolute.txt |  256 +
 .../Release/net10.0/Rochas.AI.Manager.Tests.dll    |  Bin 0 -> 79872 bytes
 .../Rochas.AI.Manager.Tests.genruntimeconfig.cache |    1 +
 .../Release/net10.0/Rochas.AI.Manager.Tests.pdb    |  Bin 0 -> 32880 bytes
 .../Rochas.AI.Manager.Tests.sourcelink.json        |    1 +
 .../net10.0/ref/Rochas.AI.Manager.Tests.dll        |  Bin 0 -> 26624 bytes
 .../net10.0/refint/Rochas.AI.Manager.Tests.dll     |  Bin 0 -> 26624 bytes
 ...ochas.AI.Manager.Tests.csproj.nuget.dgspec.json | 1777 ++++
 .../Rochas.AI.Manager.Tests.csproj.nuget.g.props   |   26 +
 .../Rochas.AI.Manager.Tests.csproj.nuget.g.targets |   15 +
 .../obj/project.assets.json                        | 4711 ++++++++++
 .../obj/project.nuget.cache                        |  203 +
 2395 files changed, 101539 insertions(+)

## f74a8d6 - Initial commit

- Commit: f74a8d60c97bc9af6a6dad771036edee1ad6ed28
- Date: 2026-10-20

 DEV-HISTORY.md | 2267 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 LAB-SETUP.md   |  511 +++++++++++++
 2 files changed, 2778 insertions(+)

## ab5ca1d - docs: DEV-HISTORY filtrado (commits AI.Manager desde a concepcao)

- Commit: ab5ca1d29b1d3a4e7f3fe88cdeb70bf41b99c157
- Date: 2026-10-20

 DEV-HISTORY.md | 2541 ++++++--------------------------------------------------
 1 file changed, 274 insertions(+), 2267 deletions(-)
