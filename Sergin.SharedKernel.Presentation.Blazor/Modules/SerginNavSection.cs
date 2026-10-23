using Sergin.SharedKernel.Modules;

namespace Sergin.SharedKernel.Presentation.Blazor.Modules;

/// <summary>
/// One module's drawer group with its entries, in drawer order.
/// </summary>
public sealed record SerginNavSection(SerginNavGroup Group, IReadOnlyCollection<SerginNavItem> Items);
