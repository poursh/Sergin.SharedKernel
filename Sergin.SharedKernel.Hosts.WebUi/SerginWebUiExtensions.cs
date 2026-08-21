using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Hosts.Dispatching;
using Sergin.SharedKernel.Hosts.WebUi;
using Sergin.SharedKernel.Hosts.WebUi.Users;
using Sergin.SharedKernel.Modules;
using Sergin.SharedKernel.Presentation;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Home;
using Sergin.SharedKernel.Presentation.Blazor.Modules;

namespace Microsoft.Extensions.Hosting;

public static class SerginWebUiExtensions
{
    /// <param name="configureHome">
    /// Chooses what the site root renders. Omit it and the root shows <c>SerginWelcome</c> under a "Home"
    /// nav entry; supply it to swap in the application's own landing page, which is the seam that lets one
    /// codebase serve a device overview in one deployment and a dashboard in another.
    /// </param>
    /// <example>
    /// <code>
    /// builder.AddSerginBlazorApp(modules, configureHome: home => home.UseComponent&lt;MyDashboard&gt;());
    /// </code>
    /// </example>
    public static WebApplicationBuilder AddSerginBlazorApp(
        this WebApplicationBuilder builder,
        IReadOnlyCollection<ISerginModule> modules,
        Action<SerginHomeBuilder>? configureHome = null)
    {
        if (!builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "The Sergin Web UI host has no authentication: every request runs as the configured development "
                + $"user 'Sergin:{DevUserOptions.SectionName}'. Refusing to start in the "
                + $"'{builder.Environment.EnvironmentName}' environment. Implement a real IUserContextFactory first.");
        }

        builder.Services.AddRazorComponents().AddInteractiveServerComponents();

        builder.Services.AddSerginBlazorKit();

        IConfigurationSection serginSection =
            builder.Configuration.GetRequiredSection(SerginCoreExtensions.SectionName);

        builder.Services.AddOptions<DevUserOptions>()
            .Bind(serginSection.GetSection(DevUserOptions.SectionName))
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<DevUserOptions>, DevUserOptionsValidator>();

        // Bound against the whole Sergin section, not a subsection: ApplicationName is a scalar sitting
        // directly under it (Sergin:ApplicationName). Sibling keys the class has no property for are ignored.
        builder.Services.AddOptions<SerginApplicationOptions>()
            .Bind(serginSection)
            .ValidateOnStart();

        builder.Services
            .AddSingleton<IValidateOptions<SerginApplicationOptions>, SerginApplicationOptionsValidator>();

        builder.Services.AddTransient<IUserContextFactory, ConfiguredUserContextFactory>();

        builder.AddSerginCore(modules);

        IReadOnlyCollection<string> schemas = [.. modules.Select(module => module.Schema)];

        builder.Services.AddOptions<DispatchModeOptions>()
            .Bind(serginSection.GetSection(DispatchModeOptions.SectionName))
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<DispatchModeOptions>>(
            _ => new DispatchModeOptionsValidator(schemas));

        builder.Services.AddSingleton<IDispatchRouteResolver>(p => new ModuleDispatchRouteResolver(
            modules.ToDictionary(module => module.ApplicationAssembly, module => module.Schema),
            p.GetRequiredService<IOptions<DispatchModeOptions>>()));

        builder.Services.AddSingleton(new SerginUiModuleCatalog([.. modules.OfType<ISerginWebUiModule>()]));

        SerginHomeBuilder homeBuilder = new();

        configureHome?.Invoke(homeBuilder);

        builder.Services.AddSingleton(homeBuilder.Build());

        return builder;
    }

    public static async Task<WebApplication> UseSerginWebUiAsync<TRootComponent>(
        this WebApplication app, IReadOnlyCollection<ISerginModule> modules)
        where TRootComponent : IComponent
    {
        SerginUiModuleCatalog catalog = app.Services.GetRequiredService<SerginUiModuleCatalog>();

        ValidateRoutePrefixes(catalog);

        if (app.Environment.IsDevelopment())
        {
            foreach (ISerginModule module in modules)
            {
                await module.MigrateAsync(app.Services);
            }
        }

        app.UseAntiforgery();

        app.MapStaticAssets();

        app.MapRazorComponents<TRootComponent>()
            .AddAdditionalAssemblies([.. catalog.RoutableAssemblies])
            .AddInteractiveServerRenderMode();

        return app;
    }

    private static void ValidateRoutePrefixes(SerginUiModuleCatalog catalog)
    {
        List<string> violations = [];

        foreach (ISerginWebUiModule module in catalog.Modules)
        {
            string prefix = $"/{module.Schema}/";

            foreach (Type component in module.UiAssembly.GetExportedTypes())
            {
                if (!typeof(IComponent).IsAssignableFrom(component))
                {
                    continue;
                }

                foreach (RouteAttribute route in component.GetCustomAttributes<RouteAttribute>(inherit: false))
                {
                    if (!route.Template.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        violations.Add($"  {component.FullName}: @page \"{route.Template}\" must start with \"{prefix}\"");
                    }
                }
            }
        }

        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                "Module routable components must sit under their module's schema prefix, because Razor @page "
                + "templates are compile-time constants and cannot be prefixed at map time the way "
                + "MapGroup(schema) prefixes minimal-API endpoints:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, violations));
        }
    }
}
