using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Modules;

namespace Sergin.SharedKernel.Presentation.Blazor.Navigation;

/// <summary>
/// Drawer entries the shell itself contributes, not any module: they join the section of <paramref name="Group"/>
/// as one more subsection under <paramref name="Caption"/>.
/// </summary>
public sealed record SerginShellNavEntry(SerginNavGroup Group, TextDefinition? Caption, IReadOnlyCollection<SerginNavItem> Items);
