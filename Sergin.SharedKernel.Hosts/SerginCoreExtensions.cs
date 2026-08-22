using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Dispatching;
using Sergin.SharedKernel.Application.Events;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Infrastracture.Data;
using Sergin.SharedKernel.Infrastructure.Data.EFCore;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Interceptors;
using Sergin.SharedKernel.Infrastructure.Dispatching;
using Sergin.SharedKernel.Infrastructure.Events;
using Sergin.SharedKernel.Infrastructure.Localizations;
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
        this TBuilder builder, IReadOnlyCollection<ISerginModule> modules)
        where TBuilder : IHostApplicationBuilder
    {
        IConfigurationSection serginSection = builder.Configuration.GetRequiredSection(SectionName);

        string[] duplicateSchemas =
        [
            .. modules.GroupBy(module => module.Schema, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
        ];

        if (duplicateSchemas.Length > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate module schema(s) registered: {string.Join(", ", duplicateSchemas)}. Each module must "
                + "appear exactly once — listing two classes for the same module runs AddServices twice.");
        }

        builder.Services.AddMediatR(options =>
        {
            foreach (ISerginModule module in modules)
            {
                options.RegisterServicesFromAssembly(module.ApplicationAssembly);
            }

            options.AddOpenBehavior(typeof(PermissionCheckPipelineBehavior<,>));
            options.AddOpenBehavior(typeof(ValidationPipelineBehavior<,>));
        });

        builder.Services.AddScoped<IEventDispatcher, DefaultEventDispatcher>();
        builder.Services.AddScoped<EventDispatcherInterceptor>();

        string connectionString = serginSection.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Connection string 'Sergin:ConnectionStrings:Database' is not configured.");

        builder.Services.AddScoped<IDbConnectionFactory>(p => new PostgresDbConnectionFactory(connectionString));

        builder.Services.AddScoped(p => p.GetRequiredService<IUserContextFactory>().CreateUserContext());

        builder.Services.AddSingleton<ILocalizer, DefaultLocalizer>();

        IReadOnlyCollection<string> schemas = [.. modules.Select(module => module.Schema)];

        builder.Services.AddOptions<DispatchModeOptions>()
            .Bind(serginSection.GetSection(DispatchModeOptions.SectionName))
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<DispatchModeOptions>>(
            _ => new DispatchModeOptionsValidator(schemas));

        IReadOnlyDictionary<Assembly, string> schemaByAssembly = modules
            .Select(module => (Assembly: module.ApplicationAssembly, module.Schema))
            .Concat(modules.Select(module => (Assembly: module.ContractsAssembly, module.Schema)))
            .DistinctBy(entry => entry.Assembly)
            .ToDictionary(entry => entry.Assembly, entry => entry.Schema);

        builder.Services.AddSingleton<IDispatchRouteResolver>(p => new ModuleDispatchRouteResolver(
            schemaByAssembly,
            p.GetRequiredService<IOptions<DispatchModeOptions>>()));

        builder.Services.AddSingleton<ISerginSender, RoutingSerginSender>();

        foreach (ISerginModule module in modules)
        {
            module.AddServices(builder.Services, serginSection);
        }

        return serginSection;
    }
}
