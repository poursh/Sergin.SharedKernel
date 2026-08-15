using System.Reflection;
using Sergin.SharedKernel.Modules;

namespace Sergin.SharedKernel.Presentation.Blazor.Modules;

public sealed class SerginUiModuleCatalog
{
    public SerginUiModuleCatalog(IReadOnlyCollection<ISerginWebUiModule> modules)
    {
        Modules = modules;
        RoutableAssemblies = [.. modules.Select(module => module.UiAssembly)];
        NavItems =
        [
            .. modules.SelectMany(module => module.NavItems)
                .OrderBy(item => item.Order)
                .ThenBy(item => item.Label, StringComparer.Ordinal)
        ];
    }

    public IReadOnlyCollection<ISerginWebUiModule> Modules { get; }

    public IReadOnlyCollection<Assembly> RoutableAssemblies { get; }

    public IReadOnlyCollection<SerginNavItem> NavItems { get; }
}
