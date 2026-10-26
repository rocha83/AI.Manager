# Arquitetura de Camadas — Rochas.AI.Manager

## Regra: Interfaces no Domain
Todas as interfaces (incluindo serviços de aplicação) ficam em `Domain/Interfaces`.
Isso centraliza contratos e especificações no domínio, evitando dependências cruzadas.

## Camadas
- **Domain**: entidades, base classes, interfaces (contratos).
- **Application**: implementação dos serviços (regras de negócio).
- **Infrastructure**: persistência (DapperRepository, AppDb, SQLite).
- **Web**: MVC (views, partials, modals, sidebar).
- **Api**: Web API + Swagger.
- **TrainingEngine**: motor de treinamento (orquestração/monitoramento).
- **Bridge (Core/Contracts/Client/Hosting)**: protocolo Responses↔chat, harness base+tails, anti-repeat e breaker; núcleo sem ASP.NET/CLI/Swagger.
- **Bridge hosts (Api/Runner)**: cascas REST+Swagger e CLI `serve`/`manifest`; não contêm regra de negócio.

## Compartilhamento
Web e Api compartilham Domain + Application + Infrastructure + TrainingEngine.
Nenhuma regra de negócio duplicada; apenas apresentação distinta.

O `AI.Manager.Bridge` é um pacote à parte: o core referencia apenas `Bridge.Contracts`
e não conhece `Domain`/`Application`; `Api`/`Runner` referenciam o core e o hosting.
O proxy legado do AI.Manager permanece até o cutover; o C4 em
`docs/ai-manager/diagrams/ai-manager-next-steps-c4.{drawio,png}` mostra o estado do porte.

## Documentos de arquitetura e pendências
- `Rochas.AI.Manager-Arquitetura v1.md` — guideline v1.2 + seção 32 (as-built).
- `Rochas.AI.Manager-Arquitetura v2.md` — arquitetura vigente (baseline, ADRs, roadmap).
- `REPORT-PENDINGS.md` — catálogo de relatórios R1–R30, facetas e recortes De/Até.
