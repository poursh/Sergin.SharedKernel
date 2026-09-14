using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Events;
using Sergin.SharedKernel.Application.Events.Integration;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Application.Times;
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
            options.AddOpenBehavior(typeof(ValidationPipelineBehavior<,>));
        });

        builder.Services.AddScoped<IEventDispatcher, DefaultEventDispatcher>();
        builder.Services.AddScoped<EventDispatcherInterceptor>();

        builder.Services.AddOptions<OutboxOptions>()
            .Bind(serginSection.GetSection(OutboxOptions.SectionName))
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<OutboxOptions>, OutboxOptionsValidator>();
        builder.Services.TryAddSingleton<IDateTimeProvider, DefaultDateTimeProvider>();

        foreach (ISerginModule module in localModules)
        {
            builder.Services.AddSingleton<IIntegrationEventSource>(new AssemblyIntegrationEventSource(module.ContractsAssembly));
        }

        foreach (ISerginRemoteModule remoteModule in remoteModules)
        {
            builder.Services.AddSingleton<IIntegrationEventSource>(new AssemblyIntegrationEventSource(remoteModule.ContractsAssembly));
        }

        builder.Services.AddSingleton<IIntegrationEventTypeRegistry, IntegrationEventTypeRegistry>();
        builder.Services.AddSingleton<IIntegrationEventSerializer, JsonIntegrationEventSerializer>();
        builder.Services.AddScoped<IIntegrationEventDispatcher, DefaultIntegrationEventDispatcher>();
        builder.Services.AddScoped<IntegrationEventContextAccessor>();
        builder.Services.AddScoped<IOutboxWriter, OutboxWriter>();
        builder.Services.AddSingleton<IOutboxRelayIdentity, OutboxRelayIdentity>();
        builder.Services.AddHostedService<OutboxRelayService>();

        foreach (ISerginModule module in localModules)
        {
            AddIntegrationEventTranslators(builder.Services, module.ApplicationAssembly);
        }

        string connectionString = serginSection.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Connection string 'Sergin:ConnectionStrings:Database' is not configured.");

        builder.Services.AddScoped<IDbConnectionFactory>(p => new PostgresDbConnectionFactory(connectionString));

        builder.Services.AddScoped<UserContextAccessor>();

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
    /// Registers every non-abstract class in <paramref name="assembly"/> against each closed
    /// <c>IIntegrationEventTranslator&lt;TDomainEvent&gt;</c> interface it implements, so a module's
    /// translators are discovered the same way its MediatR handlers are — by scanning
    /// <see cref="ISerginModule.ApplicationAssembly"/> — rather than requiring a module to hand-list them.
    /// </summary>
    private static void AddIntegrationEventTranslators(IServiceCollection services, Assembly assembly)
    {
        IEnumerable<Type> candidateTypes = assembly.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false });

        foreach (Type type in candidateTypes)
        {
            IEnumerable<Type> closedTranslatorInterfaces = type.GetInterfaces().Where(IsClosedTranslatorInterface);

            foreach (Type closedInterface in closedTranslatorInterfaces)
            {
                services.AddTransient(closedInterface, type);
            }
        }
    }

    private static bool IsClosedTranslatorInterface(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IIntegrationEventTranslator<>);
}
