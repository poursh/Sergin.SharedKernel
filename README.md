# Sergin.SharedKernel

Building-block library for the Sergin platform: cross-cutting `Domain`, `Application`, `Infrastructure`, `Presentation`, and host-bootstrap abstractions shared by every Sergin module. Part of the [Sergin](https://github.com/poursh/Sergin.MeterMinder) platform, whose **MeterMinder** module is a Head-End System (HES) for smart electricity/gas/water meters.

This repo has zero dependencies on any other Sergin repo — it's the leaf of the dependency graph and builds fully standalone.

It's consumed as a **git submodule** by:
- [Sergin.MeterMinder](https://github.com/poursh/Sergin.MeterMinder) (the host repo) — mounted at `src/SharedKernel/`.
- [Sergin.UserAccess](https://github.com/poursh/Sergin.UserAccess) (embed-only module repo) — expects this repo mounted as a sibling submodule at a matching relative path (`../../../SharedKernel` from its own project files) inside whatever host embeds it.

## Build

```bash
dotnet build Sergin.SharedKernel.slnx
```

Requires the .NET 10 SDK. `Directory.Build.props` treats every analyzer/style warning as a build error (`TreatWarningsAsErrors`, `AnalysisMode=All`, SonarAnalyzer.CSharp) — matches the convention of the other Sergin repos.

## Structure

- `Sergin.SharedKernel.Domain` — `AggregateRoot`, `Entity`, guard clauses, `RowVersion`. No dependencies.
- `Sergin.SharedKernel.Application` — command/query abstractions, pipeline behaviors, security, localization, time.
- `Sergin.SharedKernel.Infrastructure` / `Sergin.SharedKernel.Infrastructure.Data.EFCore` — `SerginDbContext` base, `IDbConnectionFactory`, EF interceptors.
- `Sergin.SharedKernel.Presentation` / `Sergin.SharedKernel.Presentation.WebApi` — `IEndpoint`, result-to-ProblemDetails mapping.
- `Sergin.SharedKernel.Hosts` / `Sergin.SharedKernel.Hosts.WebApi` — Aspire service defaults + the Sergin web-host bootstrap (`AddSerginWebApi`/`UseSerginWebApiAsync`) that every host project wires up.
- `Sergin.SharedKernel.Modules` — the `ISerginModule`/`ISerginWebApiModule` contract every module implements to register itself with a host.
- `Sergin.SharedKernel.IntegrationTests` — `SerginWebApiFactory<TEntryPoint>`, a shared `WebApplicationFactory`/Testcontainers fixture that a host repo's own integration-test project references; not a runnable test suite itself.

See `.claude/CLAUDE.md` for the full architecture reference (project layering, value-converter template, naming conventions).
