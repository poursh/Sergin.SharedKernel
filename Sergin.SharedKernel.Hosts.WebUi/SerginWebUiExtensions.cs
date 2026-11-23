using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application;
using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Application.Commands.Configuration;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Application.Tasks;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Hosts.Authentication;
using Sergin.SharedKernel.Hosts.Commands;
using Sergin.SharedKernel.Hosts.Localizations;
using Sergin.SharedKernel.Hosts.WebUi;
using Sergin.SharedKernel.Hosts.WebUi.Localizations;
using Sergin.SharedKernel.Hosts.WebUi.Users;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Aggregates;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Tasks;
using Sergin.SharedKernel.Modules;
using Sergin.SharedKernel.Presentation;
using Sergin.SharedKernel.Presentation.Blazor.Aggregates;
using Sergin.SharedKernel.Presentation.Blazor.Home;
using Sergin.SharedKernel.Presentation.Localizations;
using Sergin.SharedKernel.Presentation.Blazor.Authentication;
using Sergin.SharedKernel.Presentation.Blazor.Modules;
using Sergin.SharedKernel.Presentation.Blazor.Navigation;
using Sergin.SharedKernel.Presentation.Blazor.Tasks.Runs;

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
        IReadOnlyCollection<ISerginRemoteModule>? remoteModules = null,
        Action<SerginHomeBuilder>? configureHome = null)
    {
        IConfigurationSection serginSection =
            builder.Configuration.GetRequiredSection(SerginCoreExtensions.SectionName);

        SerginAuthOptions authOptions = builder.Services.AddSerginAuthOptions(serginSection);

        if (authOptions.Mode == SerginAuthMode.DevUser && !builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"Sergin:{SerginAuthOptions.SectionName}:Mode is '{nameof(SerginAuthMode.DevUser)}', which means "
                + "no authentication at all: every request runs as the configured development user "
                + $"'Sergin:{DevUserOptions.SectionName}'. Refusing to start in the "
                + $"'{builder.Environment.EnvironmentName}' environment. Set Mode to "
                + $"'{nameof(SerginAuthMode.Keycloak)}' and configure the realm.");
        }

        builder.Services.AddRazorComponents().AddInteractiveServerComponents();

        builder.Services.AddSerginBlazorKit();

        if (authOptions.Mode == SerginAuthMode.Keycloak)
        {
            builder.Services.AddSerginKeycloakCookieOidc(authOptions);

            // Makes Task<AuthenticationState> available to any component that wants <AuthorizeView>.
            // Page-level gating is an endpoint concern instead — see UseSerginWebUiAsync — so that
            // static assets stay anonymous and the framework's own scripts load before sign-in.
            builder.Services.AddCascadingAuthenticationState();

            builder.Services.AddSingleton(new SerginUiAuthentication(
                CanSignOut: true,
                LogoutPath: $"{SerginAuthenticationExtensions.AuthenticationPath}/logout"));
        }
        else
        {
            // Only bound in DevUser mode: a Keycloak host has no Sergin:DevUser section to validate, and
            // ValidateOnStart would fail startup over keys nothing reads.
            builder.Services.AddOptions<DevUserOptions>()
                .Bind(serginSection.GetSection(DevUserOptions.SectionName))
                .ValidateOnStart();

            builder.Services.AddSingleton<IValidateOptions<DevUserOptions>, DevUserOptionsValidator>();

            builder.Services.AddTransient<IUserContextFactory, ConfiguredUserContextFactory>();

            builder.Services.AddSingleton(SerginUiAuthentication.Disabled);
        }

        // Bound against the whole Sergin section, not a subsection: ApplicationName is a scalar sitting
        // directly under it (Sergin:ApplicationName). Sibling keys the class has no property for are ignored.
        builder.Services.AddOptions<SerginApplicationOptions>()
            .Bind(serginSection)
            .ValidateOnStart();

        builder.Services
            .AddSingleton<IValidateOptions<SerginApplicationOptions>, SerginApplicationOptionsValidator>();

        builder.AddSerginCore(modules, remoteModules);

        // The texts only a module's pages show are declared in its RCL, beside the pages; AddSerginCore has already
        // registered the ones its ConfigurationsAssembly declares. Same module name, so the same key checks apply.
        foreach (ISerginWebUiModule module in modules.OfType<ISerginWebUiModule>())
        {
            builder.Services.AddSingleton(TextSource.FromAssembly(module.UiAssembly, module.Name, TextOrigin.Ui));

            // The module's aggregate view configurations live beside its pages.
            builder.Services.AddSingleton(AggregateViewSource.FromAssembly(module.UiAssembly, module.Schema));
        }

        // Every root a view configuration declares, joined with its typed requests; resolved at start so a bad
        // configuration fails host start rather than the first render.
        builder.Services.AddSingleton(provider => AggregateViewRegistry.Build(
            provider.GetServices<AggregateViewSource>(),
            provider.GetRequiredService<CommandConfigurationRegistry>(),
            provider.GetRequiredService<AggregateFeatureRegistry>(),
            provider.GetRequiredService<TextCatalog>()));

        AddSerginRequestLocalization(builder.Services);

        builder.Services.AddSingleton(CreateUiModuleCatalog(modules, ShellEntriesFor(builder.Services)));

        SerginHomeBuilder homeBuilder = new();

        configureHome?.Invoke(homeBuilder);

        builder.Services.AddSingleton(homeBuilder.Build());

        return builder;
    }

    /// <summary>
    /// The built-in request localization with <see cref="SerginCultureProvider"/> as its only provider: no query string,
    /// no Accept-Language, so a culture is only ever the default or the caller's own choice through the switch. The
    /// supported lists follow the stored languages live; the provider has already applied the per-caller rules.
    /// </summary>
    private static void AddSerginRequestLocalization(IServiceCollection services) =>
        services.AddOptions<RequestLocalizationOptions>()
            .Configure<ITranslationCache, IOptions<SerginLocalizationOptions>>((options, cache, localization) =>
            {
                var defaultCulture = CultureInfo.GetCultureInfo(localization.Value.DefaultCulture);
                LanguageCultureList cultures = new(cache, defaultCulture, publishedOnly: false);

                options.DefaultRequestCulture = new RequestCulture(defaultCulture);
                options.SupportedCultures = cultures;
                options.SupportedUICultures = cultures;
                options.FallBackToParentCultures = false;
                options.FallBackToParentUICultures = false;
                options.RequestCultureProviders = [new SerginCultureProvider(cache, defaultCulture.Name)];
            });

    // The catalogue reads every module's NavItems eagerly, and a module's nav entries reference its permission
    // definitions: a malformed definition surfaces here, as a nested TypeInitializationException that names only
    // the navigation class. Unwrap it to the class that actually failed.
    private static SerginUiModuleCatalog CreateUiModuleCatalog(
        IReadOnlyCollection<ISerginModule> modules, IReadOnlyCollection<SerginShellNavEntry> shellEntries)
    {
        try
        {
            return new SerginUiModuleCatalog([.. modules.OfType<ISerginWebUiModule>()], shellEntries);
        }
        catch (TypeInitializationException exception)
        {
            throw StaticInitializationFailure.Describe("Composing the UI modules' navigation", exception);
        }
    }

    /// <summary>
    /// The drawer entries the shell contributes itself: the Tasks entry when a module opts into tasks (a
    /// <see cref="TasksRegistration"/> is registered), none otherwise, so a host with no task module shows no dead link.
    /// Read after <c>AddSerginCore</c>, which is what registers the modules' contexts.
    /// </summary>
    internal static IReadOnlyCollection<SerginShellNavEntry> ShellEntriesFor(IServiceCollection services) =>
        services.Any(descriptor => descriptor.ServiceType == typeof(TasksRegistration)) ? [SerginTaskNavigation.Entry] : [];

    public static async Task<WebApplication> UseSerginWebUiAsync<TRootComponent>(
        this WebApplication app, IReadOnlyCollection<ISerginModule> modules)
        where TRootComponent : IComponent
    {
        SerginUiModuleCatalog catalog = app.Services.GetRequiredService<SerginUiModuleCatalog>();

        ValidateRoutePrefixes(catalog);

        // Builds the catalogue now, so a bad registration fails start, and checks the nav against it.
        ValidateNavPermissions(catalog, app.Services.GetRequiredService<PermissionCatalog>());

        // Collects every declared text now, so a duplicate or misplaced key fails start.
        app.Services.GetRequiredService<TextCatalog>();

        AggregateFeatureGuard.EnsureApplied(app.Services);

        // Builds the registry now, so a bad ICommandConfiguration fails start rather than the first send.
        CommandConfigurationRegistry registry = app.Services.GetRequiredService<CommandConfigurationRegistry>();

        EnsureCommandTypes(app.Services, registry);

        // Builds the aggregate view models now, and checks every configured route against the routable components.
        AggregateViewGuard.Ensure(
            app.Services.GetRequiredService<AggregateViewRegistry>(), catalog.Modules.Select(module => module.UiAssembly));

        // Builds the task catalogue now, so a badly named task fails start.
        app.Services.GetRequiredService<TaskCatalog>();

        // Every field a person sees has its label declared.
        app.Services.GetRequiredService<FieldTextGuard>().EnsureLabelled(registry, app.Services.GetRequiredService<TextCatalog>());

        if (app.Environment.IsDevelopment())
        {
            foreach (ISerginModule module in modules)
            {
                await module.MigrateAsync(app.Services);
            }
        }

        SerginAuthOptions authOptions = app.Services.GetRequiredService<IOptions<SerginAuthOptions>>().Value;

        if (authOptions.Mode == SerginAuthMode.DevUser)
        {
            await SeedDevUserAsync(app.Services);
        }
        bool keycloak = authOptions.Mode == SerginAuthMode.Keycloak;

        if (keycloak)
        {
            // Fail here rather than mid-callback on someone's first login attempt.
            SerginAuthenticationExtensions.EnsureExternalIdentityResolverRegistered(app.Services);

            app.UseAuthentication();
            app.UseAuthorization();
        }

        // After authentication, so the preview policy sees the signed-in caller; before the components render.
        app.UseRequestLocalization();

        app.UseAntiforgery();

        app.MapStaticAssets();

        app.MapSerginCultureSwitch();

        RazorComponentsEndpointConventionBuilder components = app.MapRazorComponents<TRootComponent>()
            .AddAdditionalAssemblies([.. catalog.RoutableAssemblies])
            .AddInteractiveServerRenderMode();

        if (keycloak)
        {
            // Authorization sits on the component endpoints, not on a global fallback policy: a fallback
            // would also gate MapStaticAssets, so the browser could not fetch the framework's own scripts
            // until after a sign-in those scripts are needed to complete.
            components.RequireAuthorization();

            app.MapSerginLoginAndLogout();
        }

        return app;
    }

    /// <summary>
    /// Makes the configured dev user a real user holding a global administrator grant, through the user store's
    /// <see cref="IDevUserSeeder"/> when one is registered. Scoped checks answer by user id from that store, so
    /// without it the dev user would pass no check. DevUser mode only runs in Development.
    /// </summary>
    private static async Task SeedDevUserAsync(IServiceProvider services)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        if (scope.ServiceProvider.GetService<IDevUserSeeder>() is not { } seeder)
        {
            return;
        }

        DevUserOptions options = scope.ServiceProvider.GetRequiredService<IOptions<DevUserOptions>>().Value;

        await seeder.EnsureAsync(
            new DevUser(options.Id, options.UserName, options.Email, options.FirstName, options.LastName),
            CancellationToken.None);
    }

    private static void ValidateRoutePrefixes(SerginUiModuleCatalog catalog)
    {
        IReadOnlyList<string> violations = RouteViolations(catalog, typeof(SerginUiModuleCatalog).Assembly);

        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                "Module routable components must sit under their module's schema prefix, and the shell's own pages "
                + "under \"/sys/\" (or be the root \"/\"), because Razor @page templates are compile-time constants "
                + "and cannot be prefixed at map time the way MapGroup(schema) prefixes minimal-API endpoints:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, violations));
        }
    }

    /// <summary>
    /// Every routable component whose template breaks its rule: a module's under <c>/{schema}/</c>, the shell's
    /// (<paramref name="shellAssembly"/>) the root <c>/</c> or under <c>/sys/</c>, the schema no module may take.
    /// </summary>
    internal static IReadOnlyList<string> RouteViolations(SerginUiModuleCatalog catalog, Assembly shellAssembly)
    {
        List<string> violations = [];

        foreach (ISerginWebUiModule module in catalog.Modules)
        {
            string prefix = $"/{module.Schema}/";

            foreach ((Type component, RouteAttribute route) in RoutesOf(module.UiAssembly))
            {
                if (!route.Template.StartsWith(prefix, StringComparison.Ordinal))
                {
                    violations.Add($"  {component.FullName}: @page \"{route.Template}\" must start with \"{prefix}\"");
                }
            }
        }

        foreach ((Type component, RouteAttribute route) in RoutesOf(shellAssembly))
        {
            if (route.Template != "/" && !route.Template.StartsWith("/sys/", StringComparison.Ordinal))
            {
                violations.Add($"  {component.FullName}: shell @page \"{route.Template}\" must be \"/\" or start with \"/sys/\"");
            }
        }

        return violations;
    }

    private static IEnumerable<(Type Component, RouteAttribute Route)> RoutesOf(Assembly assembly) =>
        assembly.GetExportedTypes()
            .Where(type => typeof(IComponent).IsAssignableFrom(type))
            .SelectMany(type => type.GetCustomAttributes<RouteAttribute>(inherit: false).Select(route => (type, route)));

    /// <summary>
    /// Every nav entry's required permission must be in the catalogue. The nav cannot hand a typo to the menu any
    /// more, but it can name a definition no aggregate registered, which no role could grant: the entry would never
    /// show, and the page behind it would refuse everyone.
    /// </summary>
    internal static void ValidateNavPermissions(SerginUiModuleCatalog catalog, PermissionCatalog permissions)
    {
        string[] violations =
        [
            .. catalog.Modules.SelectMany(module => module.NavItems
                .Where(item => item.RequiredPermission is not null && !permissions.Contains(item.RequiredPermission))
                .Select(item => $"  {module.GetType().FullName}: nav entry \"{item.Label.Default}\" requires {item.RequiredPermission}")),
            .. catalog.ShellEntries.SelectMany(entry => entry.Items
                .Where(item => item.RequiredPermission is not null && !permissions.Contains(item.RequiredPermission))
                .Select(item => $"  shell: nav entry \"{item.Label.Default}\" requires {item.RequiredPermission}")),
        ];

        if (violations.Length > 0)
        {
            throw new InvalidOperationException(
                "Nav entries must require a permission an aggregate registers with Permissions(...), "
                + "or the permission catalogue holds nothing a role could grant for them:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, violations));
        }
    }

    // Every typed record fits the command model, and every local one names an aggregate its module's DbContext maps.
    private static void EnsureCommandTypes(IServiceProvider services, CommandConfigurationRegistry registry)
    {
        services.GetRequiredService<CommandTypeGuard>().EnsureShaped(registry, services.GetServices<ModuleConfigurationSource>());
        CommandTargetMappingGuard.EnsureMapped(services.GetRequiredService<LocalContractsAssemblies>().RequestTypes(), services);
    }
}
