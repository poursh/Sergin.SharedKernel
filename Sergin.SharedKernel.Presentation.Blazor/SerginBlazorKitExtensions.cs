using MudBlazor.Services;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Theming;
using Sergin.SharedKernel.Presentation.Blazor.Validation;

namespace Microsoft.Extensions.DependencyInjection;

public static class SerginBlazorKitExtensions
{
    public static IServiceCollection AddSerginBlazorKit(this IServiceCollection services)
    {
        services.AddMudServices();

        services.AddScoped<IUiErrorPresenter, MudUiErrorPresenter>();

        // Scoped, not singleton: it depends on IJSRuntime, which in Blazor Server is per-circuit.
        services.AddScoped<IUiThemeStore, LocalStorageThemeStore>();

        // Scoped, not singleton: it carries the caller's IUserContext into the root-provider scope it
        // opens per send. See ScopedSerginDispatcher's remarks — a singleton here strips authorization.
        services.AddScoped<ISerginDispatcher, ScopedSerginDispatcher>();

        // Scoped for the same reason: it seeds the caller's IUserContext into the scope it opens per call.
        services.AddScoped<ISerginFormValidator, ScopedSerginFormValidator>();

        return services;
    }
}
