using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Tasks.Commands;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

/// <summary>
/// The Dialog mode of a task with arguments (the default), and the one form a list's bulk start shows for every selected
/// row: the task's label as title, its <see cref="GeneratedTaskForm"/> over <see cref="Targets"/>, Start and Cancel. Start
/// starts one run over every target with the same values and closes the dialog; Cancel closes it cancelled, starting
/// nothing.
/// </summary>
public sealed partial class TaskFormDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    /// <summary>A shown task with arguments.</summary>
    [Parameter]
    [EditorRequired]
    public CommandView Command { get; set; } = default!;

    /// <summary>The records the task is started over; empty for a task over the collection.</summary>
    [Parameter]
    public IReadOnlyCollection<TaskTarget> Targets { get; set; } = [];

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    private EventCallback OnStarted => EventCallback.Factory.Create(this, Started);

    private void Started() => MudDialog.Close(DialogResult.Ok(true));

    internal void Cancel() => MudDialog.Cancel();
}

/// <summary>Opens a task's argument dialog, as the detail, list and list body views do.</summary>
internal static class TaskFormDialogs
{
    public static Task<IDialogReference> ShowTaskFormAsync(this IDialogService dialogs, CommandView command, IReadOnlyCollection<TaskTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        DialogParameters<TaskFormDialog> parameters = new()
        {
            { dialog => dialog.Command, command },
            { dialog => dialog.Targets, targets },
        };

        return dialogs.ShowAsync<TaskFormDialog>(
            string.Empty, parameters, new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true });
    }
}
