using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Configuration;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Application.Events;
using Sergin.SharedKernel.Application.Events.Integration;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Hosts;
using Sergin.SharedKernel.Hosts.Outbox;
using Sergin.SharedKernel.Infrastracture.Data;
using Sergin.SharedKernel.Infrastructure.Data.EFCore;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Interceptors;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Outbox;
using Sergin.SharedKernel.Infrastructure.Events;
using Sergin.SharedKernel.Infrastructure.Events.Integration;
using Sergin.SharedKernel.Infrastructure.Localizations;
using Sergin.SharedKernel.Infrastructure.Times;
using Sergin.SharedKernel.Modules;
using Sergin.SharedKernel.Presentation;

namespace Microsoft.Extensions.Hosting;

public static class SerginCoreExtensions
{
    public const string SectionName = "Sergin";

    /// <summary>
    /// Registers everything a Sergin host needs regardless of its presentation technology.
    /// The caller must register an <see cref="IUserContextFactory"/> — it is the one service whose
    /// implementation is host-shaped (HttpContext for the Web API, configuration for the Web UI).
    /// </summary>
    public static IConfigurationSection AddSerginCore<TBuilder>(
        this TBuilder builder,
        IReadOnlyCollection<ISerginModule> localModules,
        IReadOnlyCollection<ISerginRemoteModule>? remoteModules = null)
        where TBuilder : IHostApplicationBuilder
    {
        remoteModules ??= [];
        IConfigurationSection serginSection = builder.Configuration.GetRequiredSection(SectionName);

        string[] duplicateSchemas =
        [
            .. localModules.Select(m => m.Schema)
                .Concat(remoteModules.Select(m => m.Schema))
                .GroupBy(schema => schema, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
        ];

        if (duplicateSchemas.Length > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate module schema(s) registered: {string.Join(", ", duplicateSchemas)}. Each schema must "
                + "appear exactly once across localModules and remoteModules combined — a module cannot be both "
                + "Local and Remote in the same host, and two classes for the same schema runs AddServices twice.");
        }

        builder.Services.AddMediatR(options =>
        {
            foreach (ISerginModule module in localModules)
            {
                options.RegisterServicesFromAssembly(module.ApplicationAssembly);
            }

            // A gateway host may run zero modules locally (only Remote ones), leaving localModules empty.
            // AddMediatR throws ArgumentException("No assemblies found to scan") if it receives none, so a
            // remote module's ContractsAssembly (request/response records only, never handlers) is scanned
            // too — inert for handler discovery, but enough to keep AddMediatR from throwing at startup.
            foreach (ISerginRemoteModule remoteModule in remoteModules)
            {
                options.RegisterServicesFromAssembly(remoteModule.ContractsAssembly);
            }

            options.AddOpenBehavior(typeof(PermissionCheckPipelineBehavior<,>));
            options.AddOpenBehavior(typeof(ExpectedVersionPipelineBehavior<,>));
            options.AddOpenBehavior(typeof(ValidationPipelineBehavior<,>));
        });

        builder.Services.AddScoped<IEventDispatcher, DefaultEventDispatcher>();
        builder.Services.AddScoped<EventDispatcherInterceptor>();
        builder.Services.AddScoped<AuditStampInterceptor>();
        builder.Services.AddScoped<SoftDeleteInterceptor>();
        builder.Services.AddScoped<ExpectedVersionInterceptor>();
        builder.Services.AddScoped<RowVersionBumpInterceptor>();

        builder.Services.AddOptions<OutboxOptions>()
            .Bind(serginSection.GetSection(OutboxOptions.SectionName))
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<OutboxOptions>, OutboxOptionsValidator>();
        builder.Services.TryAddSingleton<IDateTimeProvider, DefaultDateTimeProvider>();

        // Bound against the whole Sergin section, not a subsection: TimeZone is a scalar sitting
        // directly under it, mirroring SerginApplicationOptions. This is only the fallback zone — a
        // Blazor viewer's browser-detected zone (IUiTimeZoneStore) wins whenever it's known.
        builder.Services.AddOptions<SerginTimeZoneOptions>()
            .Bind(serginSection)
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<SerginTimeZoneOptions>, SerginTimeZoneOptionsValidator>();
        builder.Services.TryAddSingleton<ILocalTimeConverter, SystemLocalTimeConverter>();

        foreach (ISerginModule module in localModules)
        {
            builder.Services.AddSingleton<IIntegrationEventSource>(new AssemblyIntegrationEventSource(module.ContractsAssembly));
        }

        foreach (ISerginRemoteModule remoteModule in remoteModules)
        {
            builder.Services.AddSingleton<IIntegrationEventSource>(new AssemblyIntegrationEventSource(remoteModule.ContractsAssembly));
        }

        // Every request's declared policy (ICommandConfiguration<T>), read by the permission and expected-version
        // behaviors. ContractsAssembly, not ApplicationAssembly, and for remote modules too: a gateway must refuse
        // a forbidden remote call before the gRPC hop, and a remote module ships nothing but its contracts. Built
        // from sources rather than here, so a test host can add its own request types; the Use…Async bootstraps
        // resolve it so a bad declaration fails host start, not the first send.
        foreach (ISerginModule module in localModules)
        {
            builder.Services.AddSingleton(CommandConfigurationSource.FromAssembly(module.ContractsAssembly));
        }

        foreach (ISerginRemoteModule remoteModule in remoteModules)
        {
            builder.Services.AddSingleton(CommandConfigurationSource.FromAssembly(remoteModule.ContractsAssembly));
        }

        builder.Services.AddSingleton(provider =>
            CommandConfigurationRegistry.FromSources(provider.GetServices<CommandConfigurationSource>()));

        builder.Services.AddSingleton<IIntegrationEventTypeRegistry, IntegrationEventTypeRegistry>();
        builder.Services.AddSingleton<IIntegrationEventSerializer, JsonIntegrationEventSerializer>();

        // The transport seam. Singleton, because the dispatcher is now a transport: the in-process one holds
        // a scope factory and opens the consumer scope itself, and a broker-backed one is a connection, which
        // has to be a singleton anyway. TryAdd, so a host that registers its own IIntegrationEventDispatcher
        // in Program.cs before AddSerginBlazorApp/AddSerginWebApi wins — a composition-time choice like
        // Local/Remote, deliberately without a Sergin:Outbox:Transport key to read at startup. The concrete
        // registration is unconditional: a broker consumer service still needs the in-process dispatcher as
        // its last mile, whatever it registered behind the interface.
        builder.Services.AddSingleton<InProcessIntegrationEventDispatcher>();
        builder.Services.TryAddSingleton<IIntegrationEventDispatcher>(
            provider => provider.GetRequiredService<InProcessIntegrationEventDispatcher>());

        builder.Services.AddScoped<IntegrationEventContextAccessor>();
        builder.Services.AddScoped<IOutboxWriter, OutboxWriter>();
        builder.Services.AddSingleton<IOutboxRelayIdentity, OutboxRelayIdentity>();
        builder.Services.AddHostedService<OutboxRelayService>();

        foreach (ISerginModule module in localModules)
        {
            AddClosedGenericImplementations(
                builder.Services, module.ApplicationAssembly, typeof(IIntegrationEventTranslator<>), ServiceLifetime.Transient);

            // FluentValidation validators, one per command/query, found the same way and handed to
            // ValidationPipelineBehavior. Scoped is FluentValidation's own default and lets a validator take
            // a scoped dependency (a query repository for a uniqueness rule) without a lifetime mismatch.
            // ApplicationAssembly only, on purpose: a remoteModules entry ships no .Application project, so
            // a gateway host never validates a remote request itself — the remote server host runs the
            // real pipeline, validators included, exactly as it would for a local call.
            AddClosedGenericImplementations(
                builder.Services, module.ApplicationAssembly, typeof(IValidator<>), ServiceLifetime.Scoped);
        }

        // Every local module's aggregate feature configurations, for the startup guard that checks them against the
        // EF models (AggregateFeatureGuard). Building it here also runs the registry's own checks — two
        // configurations for one type, a configuration without a parameterless constructor — at composition,
        // in every environment. Each module DbContext builds its own copy for the EF model; see
        // SerginDbContext.AggregateFeatures for why that one cannot come from DI.
        builder.Services.AddSingleton(
            AggregateFeatureRegistry.FromAssemblies(localModules.Select(module => module.ApplicationAssembly)));

        string connectionString = serginSection.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Connection string 'Sergin:ConnectionStrings:Database' is not configured.");

        builder.Services.AddScoped<IDbConnectionFactory>(p => new PostgresDbConnectionFactory(connectionString));

        builder.Services.AddScoped<UserContextAccessor>();

        // Seeded by each front end before a send and read back after it; see ConcurrencyContext.
        builder.Services.AddScoped<ConcurrencyContext>();

        // A seeded context wins over building a fresh one. Scopes opened from the root provider — every
        // Blazor dispatcher send — can reach neither an HttpContext nor the circuit's authentication
        // state, so the caller hands its own context down rather than having the factory guess.
        builder.Services.AddScoped(p =>
            p.GetRequiredService<UserContextAccessor>().Current
            ?? p.GetRequiredService<IUserContextFactory>().CreateUserContext());

        builder.Services.AddSingleton<ILocalizer, DefaultLocalizer>();

        foreach (ISerginModule module in localModules)
        {
            module.AddServices(builder.Services, serginSection);
        }

        foreach (ISerginRemoteModule remoteModule in remoteModules)
        {
            remoteModule.AddRemoteServices(builder.Services, serginSection);
        }

        return serginSection;
    }

    /// <summary>
    /// Registers every non-abstract class in <paramref name="assembly"/> against each closed form of
    /// <paramref name="openInterface"/> it implements, so a module's <c>IIntegrationEventTranslator&lt;TDomainEvent&gt;</c>
    /// and <c>IValidator&lt;TRequest&gt;</c> implementations are discovered the same way its MediatR handlers
    /// are — by scanning <see cref="ISerginModule.ApplicationAssembly"/> — rather than requiring a module
    /// to hand-list them.
    /// </summary>
    private static void AddClosedGenericImplementations(
        IServiceCollection services, Assembly assembly, Type openInterface, ServiceLifetime lifetime)
    {
        IEnumerable<Type> candidateTypes = assembly.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false });

        foreach (Type type in candidateTypes)
        {
            IEnumerable<Type> closedInterfaces = type.GetInterfaces()
                .Where(candidate => IsClosedFormOf(candidate, openInterface));

            foreach (Type closedInterface in closedInterfaces)
            {
                services.Add(new ServiceDescriptor(closedInterface, type, lifetime));
            }
        }
    }

    private static bool IsClosedFormOf(Type type, Type openInterface) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == openInterface;
}
