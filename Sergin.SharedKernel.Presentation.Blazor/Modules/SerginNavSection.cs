using Sergin.SharedKernel.Modules;

namespace Sergin.SharedKernel.Presentation.Blazor.Modules;

/// <summary>
/// One drawer group with its entries, split per module into <see cref="Subsections"/>, in drawer order.
/// </summary>
public sealed record SerginNavSection(SerginNavGroup Group, IReadOnlyCollection<SerginNavSubsection> Subsections)
{
    /// <summary>Every subsection's entries, flattened in drawer order.</summary>
    public IReadOnlyCollection<SerginNavItem> Items => [.. Subsections.SelectMany(subsection => subsection.Items)];

    /// <summary>
    /// Whether the drawer captions the subsections: only when the group holds entries from two or more modules, since
    /// a single module's caption would only repeat the group's title.
    /// </summary>
    public bool ShowsCaptions => Subsections.Count > 1;
}

/// <summary>
/// One module's entries inside a shared group, under its optional caption.
/// </summary>
public sealed record SerginNavSubsection(TextDefinition? Caption, IReadOnlyCollection<SerginNavItem> Items);
