using MudBlazor;
using MudBlazor.Services;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Localizations;
using Sergin.SharedKernel.Presentation.Blazor.Security;
using Sergin.SharedKernel.Presentation.Blazor.Theming;
using Sergin.SharedKernel.Presentation.Blazor.Times;
using Sergin.SharedKernel.Presentation.Blazor.Trees;
using Sergin.SharedKernel.Presentation.Blazor.Validation;

namespace Microsoft.Extensions.DependencyInjection;

public static class SerginBlazorKitExtensions
{
    public static IServiceCollection AddSerginBlazorKit(this IServiceCollection services)
    {
        services.AddMudServices(config => config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight);

        // After AddMudServices so it replaces MudBlazor's own localizer: its texts come from the catalog.
        services.AddTransient<MudLocalizer, SerginMudLocalizer>();
        services.AddSingleton(MudBlazorTexts.Source);

        // The shell's own words: only the Blazor kit shows them, so they are declared here, not in Application.
        // FromTypes reads only the types it is given, so the nested parts (Layout, Navigation, …) are passed too.
        services.AddSingleton(TextSource.FromTypes("common", TextOrigin.Ui, [typeof(ShellTexts), .. typeof(ShellTexts).GetNestedTypes()]));

        services.AddScoped<IUiErrorPresenter, MudUiErrorPresenter>();

        // Scoped, not singleton: it depends on IJSRuntime, which in Blazor Server is per-circuit.
        services.AddScoped<IUiThemeStore, LocalStorageThemeStore>();

        // Scoped for the same reason; the session holds the circuit's scope choice beside it.
        services.AddScoped<IUiScopeStore, LocalStorageScopeStore>();
        services.AddScoped<SerginScopeSession>();

        // Scoped for the same reason. Browser-detected wins; falls back to the app-wide Sergin:TimeZone
        // default (bound below) when JS hasn't run yet or the browser's reported id doesn't resolve.
        services.AddScoped<IUiTimeZoneStore, BrowserTimeZoneStore>();

        // Scoped for the same reason: a tree page's panel width, hidden state and last group.
        services.AddScoped<IUiTreeLayoutStore, LocalStorageTreeLayoutStore>();

        // Scoped, not singleton: it carries the caller's IUserContext into the root-provider scope it
        // opens per send. See ScopedSerginDispatcher's remarks — a singleton here strips authorization.
        services.AddScoped<ISerginDispatcher, ScopedSerginDispatcher>();

        // Scoped for the same reason: it seeds the caller's IUserContext into the scope it opens per call.
        services.AddScoped<ISerginFormValidator, ScopedSerginFormValidator>();

        // Singleton: it only reads the culture of the calling thread and the process-wide translation cache.
        services.AddSingleton<SerginDocumentCulture>();

        return services;
    }
}
