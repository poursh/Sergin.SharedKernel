# Sergin.SharedKernel

**The building blocks every Sergin module and host is made of: domain primitives, CQRS abstractions, the MediatR pipeline, EF Core and Dapper plumbing, the event and outbox pipe, the Blazor shell, and the bootstraps that assemble a host from a list of modules.**

[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)
[![Projects](https://img.shields.io/badge/projects-15-4D4D4D)](#what-is-inside)
[![License: MIT](https://img.shields.io/badge/License-MIT-2E7D32)](LICENSE)

This is the leaf of the Sergin dependency graph. It depends on no other Sergin repository and is not runnable on its own — it is a library, consumed as a **git submodule**:

| Consumer | Mounted at | Notes |
|---|---|---|
| [Sergin.MeterMinder](https://github.com/poursh/Sergin.MeterMinder) | `src/SharedKernel/` | The host repository. Its integration tests are where this library is exercised end to end. |
| [Sergin.UserAccess](https://github.com/poursh/Sergin.UserAccess) | `../../../SharedKernel` from its project files | An embed-only module. It expects this repository mounted as a sibling submodule at that relative path inside whatever host embeds it. |

Because every consumer pins a commit of this repository, a change to a public contract here (`ISerginModule`, `AggregateRoot`, `ICommand`/`IQuery`, `IEndpoint`, …) means bumping the submodule pointer — and re-running the tests — in each of them.

## Build

This repository builds only inside a host. It has no `Directory.Build.props` and no `Directory.Packages.props` of its own: the host's files, found by MSBuild walking up from `src/SharedKernel/`, set the analyzer rules (`TreatWarningsAsErrors`, `AnalysisMode=All`, SonarAnalyzer.CSharp, `EnforceCodeStyleInBuild`) and pin every package version. Build and test from the host checkout:

```bash
git clone --recurse-submodules https://github.com/poursh/Sergin.MeterMinder.git
cd Sergin.MeterMinder
dotnet build Sergin.MeterMinder.slnx
dotnet test tests/Sergin.MeterMinder.IntegrationTests.All/Sergin.MeterMinder.IntegrationTests.All.csproj   # needs Docker
```

`Sergin.SharedKernel.slnx` is kept for opening the projects in an IDE; `dotnet build` on it from this directory fails with NU1010. A new package goes in as a version-less `PackageReference` here plus a `PackageVersion` entry in the host's `Directory.Packages.props`. Requires the .NET 10 SDK. There is no test project with its own tests here; `Sergin.SharedKernel.IntegrationTests` is a fixture a host's test project references.

## What is inside

Fifteen projects, layered the same way a module is.

### Domain and Application

| Project | What it holds |
|---|---|
| `Sergin.SharedKernel.Domain` | `AggregateRoot<TId>`, `Entity`, `RowVersion`, repository contracts (`IRepository<TAggregateRoot, TId>`, `IUniqueKeyRepository<TKey>`), `Ardalis.GuardClauses` imported globally, and the security primitives: `Permission` (the stored-grant value object, validating the `permission.<parts>` format — here, not in Application, so an aggregate can hold it), `PermissionDefinition` (a named, labelled permission a module declares once per aggregate) and `AuthorizationScope` (the well-known root scope). Zero dependencies. |
| `Sergin.SharedKernel.Application` | The CQRS contracts (`ICommand`/`ICommandHandler`, `IQuery`/`IQueryHandler`, `IListQuery`/`IListQueryHandler`, the **abstract** `ListQuery` family, `ListQueryResponse<T>`, `ListFieldSet` and `ListQueryValidator<TQuery>` for declared sort/filter fields, `IUnitOfWork`), the three MediatR behaviors (`PermissionCheckPipelineBehavior`, `ExpectedVersionPipelineBehavior`, `ValidationPipelineBehavior`, in that order), FluentValidation repository rules (`MustExistIn`, `MustBeUniqueIn`), the security seam (`IUserContext` — identity only, no permissions — `ClaimsPrincipalUserContext`, `UserContextAccessor`, `ISystemUserContext`, `IExternalIdentityResolver`, `IDevUserSeeder`, `IUserDirectory`), scoped authorization (`IScopeAuthorization`, `IScopedPermissionChecker`, `IAuthorizationScopeContext`, `ScopeFilter`, `PermissionCatalog`), request policy (`ICommandConfiguration<TCommand>` / `<TCommand, TAggregateRoot>`, `CommandConfigurationRegistry`), aggregate features (`IAggregateFeatureConfiguration<T>`: `Audited`, `SoftDeletable`, `Versioned`, `Scoped`, `Permissions`), optimistic concurrency (`ConcurrencyContext`, `VersionedQueryHandler`, `VersionErrors`), the domain-event envelope (`DomainEventNotification<TEvent>`, `IDomainEventHandler<TEvent>`), and the integration-event contracts of the outbox (`IIntegrationEvent`, `[IntegrationEventName]`, `IIntegrationEventTranslator<T>`, `IIntegrationEventHandler<T>`, `InboxIntegrationEventHandler<,>`, `IntegrationEventEnvelope`, `IIntegrationEventDispatcher`). MediatR-visible, EF-free. |

### Infrastructure

| Project | What it holds |
|---|---|
| `Sergin.SharedKernel.Infrastructure` | `DefaultEventDispatcher` (publishes domain events through `IPublisher`), `DefaultLocalizer`, `DefaultDateTimeProvider`, and the outbox's in-process half: `IntegrationEventTypeRegistry` (fails host start on a missing or duplicate wire name), `AssemblyIntegrationEventSource`, `JsonIntegrationEventSerializer`, and `InProcessIntegrationEventDispatcher` — the default transport and the consumer-side last mile a broker adapter would call. |
| `Sergin.SharedKernel.Infrastructure.Data.EFCore` | `SerginDbContext`, `DefaultDbConnectionFactory`, the `AddModuleDbContext<TContext, TIContext, TIUnitOfWork>` helper (schema, per-schema migrations history, interceptor, and — when the context implements `IOutboxDbContext` — that module's `IInbox` and `IOutboxRelaySource`), `MigrateDbContextAsync`, `EfRepository<TAggregateRoot, TId>` (the write-side repository base), the save interceptors in their registration order — `ExpectedVersion`, `EventDispatcher` (dispatches from `SavingChangesAsync`, before the transaction, and writes outbox rows in the same save), `ScopeStamp`, `RecordScope`, `AuditStamp`, `SoftDelete`, `RowVersionBump` — the `AggregateFeatureConvention` that turns each module's aggregate features into shadow columns, query filters and indexes, `AggregateFeatureGuard` (refuses host start when a configuration and a model disagree), `ListQueryColumns`/`ListQuerySql` (declared list fields to parameterised SQL), and `Outbox/`: `OutboxMessage`, `InboxMessage`, `ApplyOutbox()`, `OutboxWriter`, `EfInbox`, `OutboxOptions`, `OutboxRelaySource` (`FOR UPDATE SKIP LOCKED`, backoff `min(2^attempts, 300)` s, retention purge). |
| `Sergin.SharedKernel.Infrastracture.Data` | Near-empty leaf holding `IDbConnectionFactory`. The misspelling is the real project name — renaming it means touching every consumer's `ProjectReference`. |
| `Sergin.SharedKernel.Infrastracture.WebApi` | `InternalUserContextFactory`, the `HttpContext`-derived factory an API host uses in `DevUser` mode; a `SYSTEM`/`ANONYMOUS` stub, unused while there is no API host (in `Keycloak` mode an API host uses the JWT-bearer claims factory instead). Same spelling as the project above. |

### Presentation

| Project | What it holds |
|---|---|
| `Sergin.SharedKernel.Presentation` | `SerginProblem` and `SerginProblemFactory` — the `HttpContext`-free mapping from an `Error` to status, title and detail that both front ends share, so API and UI say the same thing for the same error code — and `SerginApplicationOptions` (`Sergin:ApplicationName`). |
| `Sergin.SharedKernel.Presentation.WebApi` | `IEndpoint` and `ApiProblemResults`, the `ErrorOr` → ProblemDetails mapping built on `SerginProblem`. |
| `Sergin.SharedKernel.Presentation.Grpc` | The Remote-dispatch contract: `IRemoteInvoker<TRequest, TResponse>` (what a module's gRPC client implements) and `RemoteForwardingHandler<TRequest, TResponse>` (wraps an invoker as a real MediatR handler, so a Remote module's requests run the same pipeline a Local one does). Compiles `error.proto` into `ErrorReply`/`ProtoErrorType`, so an `ErrorOr` failure survives the wire in both directions. |
| `Sergin.SharedKernel.Presentation.Blazor` | The shell, registered by `AddSerginBlazorKit()`: `ISerginDispatcher`/`ScopedSerginDispatcher` (a fresh DI scope per `SendAsync`, seeded with the caller's `IUserContext` — registered **scoped**, and a singleton would hand every send an anonymous user), `IUiErrorPresenter` over MudBlazor, `SerginUiModuleCatalog`, the home slot (`SerginHome`/`SerginHomePage`, so a host supplies the `/` component instead of declaring its own `@page "/"`), `IUiThemeStore`/`LocalStorageThemeStore`, `IUiTimeZoneStore` (browser-detected zone, `Sergin:TimeZone` as fallback), `SerginUiAuthentication`, the layout with its scope picker (`SerginScopePicker`, `SerginScopeSession`), the nav menu grouped by module, breadcrumbs, `ISerginFormValidator` (runs the pipeline's own validator field by field from a `MudForm`), the page/view split (`SerginPage` hosting `SerginAggregateListView`, `SerginAggregateDetailView`, `SerginEntityListView`, `SerginEntityDetailView`, `SerginFormView`), server-paged lists with search, sort, filters and bulk actions, and the info dialog (scope, version, audit). |

### Modules and Hosts

| Project | What it holds |
|---|---|
| `Sergin.SharedKernel.Modules` | The contracts a module implements to register itself: `ISerginModule` (schema, assemblies, `AddServices`, `MigrateAsync`), `ISerginWebApiModule` (`MapEndpoints`), `ISerginWebUiModule` (`UiAssembly`, `NavItems`), and the lighter `ISerginRemoteModule` for a module a host calls over gRPC instead of running locally. |
| `Sergin.SharedKernel.Hosts` | Aspire service defaults (`AddServiceDefaults`), the Keycloak/OIDC wiring (`SerginAuthOptions`, `SerginAuthenticationExtensions`, `CookieOidcRefresher`, `ClaimsPrincipalUserContextFactory`), the outbox relay (`OutboxRelayService`, `OutboxRelayIdentity`, `OutboxOptionsValidator`), and **`AddSerginCore`** — see below. |
| `Sergin.SharedKernel.Hosts.WebApi` | `AddSerginWebApi`/`UseSerginWebApiAsync`: OpenAPI, an `HttpContext`-derived user context, and endpoint mapping under `MapGroup(schema)`, layered on `AddSerginCore`. Compiles and is kept current; no host calls it today. |
| `Sergin.SharedKernel.Hosts.WebUi` | `AddSerginBlazorApp`/`UseSerginWebUiAsync`: Razor Components, the `Sergin:Auth:Mode` branch (`Keycloak` adds cookie and OIDC handlers; `DevUser` binds `Sergin:DevUser` and refuses to start outside Development), migrations in Development, and a startup guard that throws unless every module page route starts with `/{schema}/`. Targets `Microsoft.NET.Sdk.Razor` with no `.razor` files of its own, so static web assets keep flowing through it from the module RCLs. |
| `Sergin.SharedKernel.IntegrationTests` | `SerginWebApiFactory<TEntryPoint>` — a `WebApplicationFactory` that starts a `postgres:17` Testcontainer and sets the connection-string environment variable before the host builds. Presentation-agnostic despite the name; the Blazor host uses it unchanged. |

## What `AddSerginCore` gives a host

`AddSerginCore<TBuilder>(localModules, remoteModules = null)` is the presentation-agnostic half of both bootstraps. It registers, for every host:

- MediatR scanning each Local module's `ApplicationAssembly`, with the permission, expected-version and validation behaviors in that order, and the FluentValidation validators from the same scan.
- Scoped authorization: `IScopeAuthorization`, the `PermissionCatalog`, and fail-closed defaults (`DenyAllScopedPermissionChecker`, `RootOnlyScopeDirectory`, `EmptyUserDirectory`) that the user-store module's own registrations replace.
- The domain-event dispatcher and interceptor, `IDbConnectionFactory`, the localizer, `IDateTimeProvider`.
- The scoped `IUserContext` — preferring one seeded through `UserContextAccessor`, otherwise built by whatever `IUserContextFactory` the host registered. **It registers no factory itself**; that is the one host-shaped decision, and each bootstrap makes it before calling in.
- The `AddServices` loop over Local modules and the `AddRemoteServices` loop over Remote ones, guarded together so no two modules claim a schema.
- The command configuration registry (from every Local and Remote module's `ConfigurationsAssembly`) and the aggregate feature registry (from every Local one). Start is refused if any command or aggregate configuration sits in a module's Application or Contracts assembly instead: only `ConfigurationsAssembly` is read.
- The outbox: `Sergin:Outbox` options validated at start, an integration-event source per module's `ContractsAssembly`, the registry and serializer, the translator scan, the relay identity, `OutboxRelayService` as a hosted service, and the transport seam — `InProcessIntegrationEventDispatcher` by its concrete type, then `TryAddSingleton<IIntegrationEventDispatcher>` so a broker-backed dispatcher registered earlier in `Program.cs` wins.

Local versus Remote is which collection a module is passed in. There is no configuration key for it, and none for the transport either — both are composition-time choices.

## Things to know before changing it

- **`ListQuery` is abstract on purpose.** Every list feature declares its own request record; that record is what a command configuration requires a permission for and what a Remote module registers a forwarding handler against.
- **`ScopedSerginDispatcher` is scoped, not singleton.** The scope it opens comes from the root provider and can reach no circuit state, so it carries the caller's `IUserContext` in by hand. Making it a singleton again reintroduces the anonymous-user bug the host's `DispatcherUserContextTests` guards.
- **`EventDispatcherInterceptor` runs before the transaction.** Moving it back to `SavedChangesAsync` reintroduces "caller sees failure, row already durable". The synchronous `SaveChanges` throws when events are pending rather than dropping them.
- **The outbox is opt-in per module.** A `DbContext` implements `IOutboxDbContext` and calls `ApplyOutbox()`; nothing is mapped unconditionally, because EF would warn about pending model changes for any context that has not migrated the tables in.
- **Delivery is at-least-once and unordered.** Derive a consumer from `InboxIntegrationEventHandler<TEvent, TUnitOfWork>` for dedup, or own the idempotency yourself. Two transports in one process is double delivery — all in-process or all through the broker.

The full reference — every project's contents, the value-converter template, the naming rules — is in [`.claude/CLAUDE.md`](.claude/CLAUDE.md). The designs behind the dispatch contract, the contracts split and the outbox live in the host repository under `docs/superpowers/specs/`.

## License

[MIT](LICENSE) © Pejman Pourshirazi.
