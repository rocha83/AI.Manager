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
