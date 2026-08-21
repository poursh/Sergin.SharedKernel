using MudBlazor.Services;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Theming;

namespace Microsoft.Extensions.DependencyInjection;

public static class SerginBlazorKitExtensions
{
    public static IServiceCollection AddSerginBlazorKit(this IServiceCollection services)
    {
        services.AddMudServices();

        services.AddSingleton<ISerginUiDispatcher, RoutingSerginUiDispatcher>();
        services.AddScoped<IUiErrorPresenter, MudUiErrorPresenter>();

        // Scoped, not singleton: it depends on IJSRuntime, which in Blazor Server is per-circuit.
        services.AddScoped<IUiThemeStore, LocalStorageThemeStore>();

        return services;
    }
}
