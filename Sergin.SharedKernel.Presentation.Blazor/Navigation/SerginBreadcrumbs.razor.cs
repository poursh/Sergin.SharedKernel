using Microsoft.AspNetCore.Components;

namespace Sergin.SharedKernel.Presentation.Blazor.Navigation;

/// <summary>
/// The breadcrumb strip alone: a <see cref="SerginPageHeader"/> with no icon buttons.
/// </summary>
public sealed partial class SerginBreadcrumbs
{
    /// <summary>
    /// Identifies the outlet in <c>SerginMainLayout</c> the page header projects into. An object rather than a
    /// section name, so no other section can collide with it by spelling the same string.
    /// </summary>
    public static object SectionId { get; } = new();

    /// <summary>
    /// The page's steps below home, first to last; the last one is the current page.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<SerginBreadcrumb> Trail { get; set; } = [];
}
