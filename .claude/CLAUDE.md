# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Sergin.SharedKernel is the building-block library for the **Sergin** platform — a .NET 10 modular monolith HES (Head-End System) for utility smart metering (electricity/gas/water meters). This repo holds the framework-level abstractions — `Domain`, `Application`, `Infrastructure`, `Presentation`, and host-bootstrap plumbing — shared by every Sergin module.

**This repo has zero dependencies on any other Sergin repo** and is not runnable on its own — it's a pure library. It's consumed as a **git submodule**:
- By [Sergin.MeterMinder](https://github.com/poursh/Sergin.MeterMinder) (the host repo) at `src/SharedKernel/`.
- By [Sergin.UserAccess](https://github.com/poursh/Sergin.UserAccess) (an embed-only module repo) at a matching relative path once UserAccess is itself embedded inside a host.

Because of this, changes here ripple into every module and host repo that pins a commit of this one. Treat public types/contracts (`ISerginModule`, `ISerginWebApiModule`, `AggregateRoot`, `ICommand`/`IQuery`, `IEndpoint`) as a stable API surface — a breaking change here requires bumping the submodule pointer (and re-testing) in every consumer.

## Commands

```bash
dotnet build Sergin.SharedKernel.slnx
```

Requires the .NET 10 SDK. There is no test project in this repo today.

## Critical build constraint

`Directory.Build.props` sets `TreatWarningsAsErrors=true`, `AnalysisMode=All`, and enables **SonarAnalyzer.CSharp** + `EnforceCodeStyleInBuild`. Any analyzer warning, style violation, or nullable warning **fails the build**. Nullable and implicit usings are enabled solution-wide. Write code that passes analysis cleanly the first time.

**Central Package Management is on.** `Directory.Packages.props` holds every package version as a `<PackageVersion>` entry, trimmed to just the packages this repo's 11 projects actually use (a subset of the full Sergin monorepo's package list — host- and test-only packages like `Testcontainers.PostgreSql`/`xunit`/`Microsoft.EntityFrameworkCore.Design` were dropped when this repo was extracted). `PackageReference` items in `.csproj` files carry **no `Version` attribute** — add new packages version-less and add the matching `<PackageVersion>` entry here, keeping the list alphabetical.

## Project layering

- **`Sergin.SharedKernel.Domain`** — `AggregateRoot<TId>`, `Entity`, strongly-typed-ID conventions, `Ardalis.GuardClauses` (globally imported but **present-but-unused** — no constructor/factory in the consuming modules actually calls a guard clause yet), `RowVersion` (exists for optimistic concurrency, but no aggregate carries one today). Zero dependencies — the leaf of the whole Sergin dependency graph.
- **`Sergin.SharedKernel.Application`** — `ICommand`/`ICommandHandler`, `IQuery`/`IQueryHandler`, `IListQuery`/`IListQueryHandler`, `ListQueryResponse<T>`, `IUnitOfWork`, the MediatR pipeline behaviors (`PermissionCheckPipelineBehavior` enforces `[RequiredPermissionsAttribute]` against `IUserContext`; `ValidationPipelineBehavior` runs an optional FluentValidation `IValidator<TRequest>` if one is registered — order matters, permission check runs first), domain-event contracts (`IDomainEvent`, `IEventDispatcher` — dispatched by `EventDispatcherInterceptor` on EF `SaveChanges`, but **no consuming aggregate calls `Raise(...)` yet** — present-but-unused), time abstraction (`IDateTimeProvider`).
- **`Sergin.SharedKernel.Infrastructure`** — `SerginDbContext` base class, `IDbConnectionFactory` implementation, `EventDispatcherInterceptor`, `DefaultDateTimeProvider`.
- **`Sergin.SharedKernel.Infrastracture.Data`** *(sic — real project name, matches the typo)* — a near-empty leaf project; don't "fix" the typo by renaming without checking every consumer's `ProjectReference` path first.
- **`Sergin.SharedKernel.Infrastructure.Data.EFCore`** *(spelled correctly here — the inconsistency with the project above is real, not a documentation error)* — EF Core-specific building blocks: the `AddModuleDbContext<TContext, TIContext, TIUnitOfWork>` helper (wires a module's `DbContext`, schema, and per-schema migrations-history table) and `MigrateDbContextAsync<TContext>()`.
- **`Sergin.SharedKernel.Presentation`** / **`Sergin.SharedKernel.Presentation.WebApi`** — `IEndpoint` (minimal-API endpoint contract), `ErrorOr` result-to-`ProblemDetails` mapping (`ToApiResult()`), `ApiProblemResults` (localizes not-found/validation responses on `error.Code`).
- **`Sergin.SharedKernel.Infrastracture.WebApi`** *(sic, matches the typo above)* — web-specific infrastructure glue.
- **`Sergin.SharedKernel.Hosts`** — Aspire service defaults (OpenTelemetry, health checks, resilience, service discovery) via `AddServiceDefaults`.
- **`Sergin.SharedKernel.Hosts.WebApi`** (namespace `Microsoft.Extensions.Hosting`) — `SerginWebApiExtensions`: `AddSerginWebApi` registers MediatR (scanning every module's `ApplicationAssembly`) + the pipeline behaviors above, OpenAPI, the event dispatcher/interceptor, `IDbConnectionFactory`, user context, localizer, then loops `module.AddServices(...)`; `UseSerginWebApiAsync` migrates every module (Development environment only), maps each `ISerginWebApiModule`'s endpoints under `MapGroup(module.Schema)`, then maps OpenAPI and (Development-only) Scalar.
- **`Sergin.SharedKernel.Modules`** — the module contract every Sergin module implements: `ISerginModule` (core: `MigrateAsync`, `AddServices`) and `ISerginWebApiModule` (adds `Schema`, `ApplicationAssembly`, `MapEndpoints`). This is the seam a host uses to compose modules — see `docs/superpowers/specs/2026-07-26-module-registration-design.md` in the [Sergin.MeterMinder](https://github.com/poursh/Sergin.MeterMinder) repo for the original design rationale (that repo is where the doc lives; it predates this repo's extraction).

## Value converter template

For a wrapped value object (used by every module's `.Infrastructure.Data` project):
```csharp
internal sealed class FooConverter : ValueConverter<Foo, TPrimitive>
{
    private static readonly ConverterMappingHints defaultHints = new();
    public FooConverter() : this(null) { }
    public FooConverter(ConverterMappingHints? mappingHints)
        : base(x => x.Value, x => new Foo(x), defaultHints.With(mappingHints)) { }
}
```
For a **nullable** wrapped value object, both type params and both conversion expressions get a null ternary instead (`ValueConverter<Foo?, TPrimitive?>`, `x => x == null ? null : x.Value` / `x => x == null ? null : new Foo(x)`).

## Cross-cutting conventions that originate here

- **Results**: `ErrorOr<T>` (global-imported in `.Application`/`.Domain` consumers). `.ToApiResult()` converts to an `IResult`/ProblemDetails. The not-found idiom is bare `Error.NotFound()` — no custom code/description, since `ApiProblemResults` localizes on `error.Code` and every not-found response currently renders identical generic text regardless of aggregate.
- **Permissions**: `[RequiredPermissions("permission.<schema>.<resource>.<action>")]` on a command/query record, enforced by `PermissionCheckPipelineBehavior`. Opt-in per slice — its absence on a consumer's handler is not necessarily an oversight.
- **`UseSnakeCaseNamingConvention()`** (via `EFCore.NamingConventions`) maps PascalCase members to snake_case columns — applied per-module in each consumer's `DbContext`, not here, but the package reference lives in this repo's `Directory.Packages.props`.
- **Local variable typing**: declare a local as the narrowest interface its actual usage needs (e.g. `IReadOnlyCollection<T>` over `List<T>` when only ever handed to something expecting that interface). Collection expressions (`[.. ...]`) can target an interface directly since C# 12.

Each consuming project has a `GlobalUsings.cs`; check it before adding `using` statements that may already be global.
