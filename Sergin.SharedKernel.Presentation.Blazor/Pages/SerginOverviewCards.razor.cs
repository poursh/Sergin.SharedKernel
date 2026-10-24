using Microsoft.AspNetCore.Components;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// The content of a <see cref="SerginAggregatePage"/>'s <c>Overview</c> slot: a grid of <see cref="SerginOverviewCard"/>s,
/// so every aggregate page shows its figures in the same cards on the same grid.
/// </summary>
public sealed partial class SerginOverviewCards
{
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
