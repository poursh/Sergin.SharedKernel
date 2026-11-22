using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Tasks.Commands;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

/// <summary>
/// The Inline mode of a task with arguments: an outlined, collapsible section of the detail view (a task over the record)
/// or the list view (a task over the collection), titled with the task's label, holding its <see cref="GeneratedTaskForm"/>.
/// It has no action-bar button; after Start it keeps the values entered, and it shows the spinner while the host says the
/// task's run is going.
/// </summary>
public sealed partial class GeneratedTaskSection
{
    [Parameter]
    [EditorRequired]
    public CommandView Command { get; set; } = default!;

    /// <summary>The record the task is started over; empty for a task over the collection.</summary>
    [Parameter]
    public IReadOnlyCollection<TaskTarget> Targets { get; set; } = [];

    [Parameter]
    public EventCallback OnStarted { get; set; }

    [Parameter]
    public bool Busy { get; set; }
}
