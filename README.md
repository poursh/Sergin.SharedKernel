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

14 projects in total:

- `Sergin.SharedKernel.Domain` — `AggregateRoot`, `Entity`, guard clauses, `RowVersion`. No dependencies.
- `Sergin.SharedKernel.Application` — command/query abstractions, pipeline behaviors, security, localization, time.
- `Sergin.SharedKernel.Infrastructure` — `SerginDbContext` base, `IDbConnectionFactory` implementation, EF interceptors, `DefaultDateTimeProvider`.
- `Sergin.SharedKernel.Infrastracture.Data` *(sic — real, existing project name)* — near-empty leaf project holding the `IDbConnectionFactory` interface itself. Don't "fix" the typo without checking every consumer's `ProjectReference` path first.
- `Sergin.SharedKernel.Infrastructure.Data.EFCore` *(spelled correctly — the inconsistency with the project above is real)* — the `AddModuleDbContext<TContext, TIContext, TIUnitOfWork>` helper (wires a module's `DbContext`, schema, and per-schema migrations-history table) and `MigrateDbContextAsync<TContext>()`.
- `Sergin.SharedKernel.Presentation` — no longer the empty placeholder it once was: `SerginProblem`/`SerginProblemFactory`, an `HttpContext`-free mapping from an `Error` to a status code/title/detail, shared by both the API and the UI so the two render identical error text for the same error code.
- `Sergin.SharedKernel.Presentation.WebApi` — `IEndpoint`, result-to-ProblemDetails mapping (`ApiProblemResults`, now built on the `SerginProblem` mapping above).
- `Sergin.SharedKernel.Presentation.Blazor` — the shared Blazor kit: the `ISerginUiDispatcher` MediatR dispatcher, MudBlazor-backed error presentation, the module nav/route catalog, and the shared shell layout/nav-menu components every module's UI reuses.
- `Sergin.SharedKernel.Infrastracture.WebApi` *(sic, matches the typo above)* — web-specific infrastructure glue; currently just `InternalUserContextFactory` (a `SYSTEM`/`ANONYMOUS` stub — real auth isn't wired yet).
- `Sergin.SharedKernel.Hosts` — Aspire service defaults, plus `AddSerginCore`: the MediatR, pipeline-behavior, event-dispatcher, and localizer registrations every host needs regardless of whether it's an API or a UI.
- `Sergin.SharedKernel.Hosts.WebApi` — the Sergin Web API bootstrap (`AddSerginWebApi`/`UseSerginWebApiAsync`) that layers OpenAPI, an `HttpContext`-derived user context, and endpoint mapping on top of `AddSerginCore`.
- `Sergin.SharedKernel.Hosts.WebUi` — the Sergin Blazor Server bootstrap (`AddSerginWebUi`/`UseSerginWebUiAsync`) that layers Razor Components, a configuration-driven dev user, and a module route-prefix guard on top of `AddSerginCore`; refuses to start outside Development, since it has no real authentication yet. Targets `Microsoft.NET.Sdk.Razor` even though it holds no `.razor` files of its own — a plain SDK doesn't import `Microsoft.NET.Sdk.StaticWebAssets`, so it would silently drop the static web assets (MudBlazor's CSS/JS, etc.) that flow through from the Razor class libraries it references; `Sdk.Razor` keeps it a working link in that propagation chain.
- `Sergin.SharedKernel.Modules` — the `ISerginModule`/`ISerginWebApiModule`/`ISerginWebUiModule` contracts every module implements to register itself with a host: core capabilities on `ISerginModule`, minimal-API endpoints on `ISerginWebApiModule`, Blazor pages/nav on `ISerginWebUiModule`.
- `Sergin.SharedKernel.IntegrationTests` — `SerginWebApiFactory<TEntryPoint>`, a shared `WebApplicationFactory`/Testcontainers fixture that a host repo's own integration-test project references; not a runnable test suite itself.

See `.claude/CLAUDE.md` for the full architecture reference (project layering, value-converter template, naming conventions).

## License

[MIT](LICENSE) © Pejman Pourshirazi.
