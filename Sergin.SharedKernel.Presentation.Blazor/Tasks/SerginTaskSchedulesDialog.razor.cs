using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Tasks;
using Sergin.SharedKernel.Presentation.Blazor.Scopes;

namespace Sergin.SharedKernel.Presentation.Blazor.Tasks;

/// <summary>
/// The schedules of one collection task (<c>ITaskCommand&lt;TRoot&gt;</c>) in one scope, opened from the list header's
/// "Schedules" entry: a <see cref="SerginTaskSchedulesSection"/> with no record, so the list stays where it was beneath.
/// Its title names the task by its view label (its stable name when no view names it) and the scope by its name.
/// </summary>
public sealed partial class SerginTaskSchedulesDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    /// <summary>The owning module's schema.</summary>
    [Parameter]
    [EditorRequired]
    public string Schema { get; set; } = string.Empty;

    /// <summary>The collection task's stable name.</summary>
    [Parameter]
    [EditorRequired]
    public string TaskName { get; set; } = string.Empty;

    /// <summary>The scope whose schedules of the task are listed.</summary>
    [Parameter]
    [EditorRequired]
    public Guid ScopeId { get; set; }

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    [Inject]
    private TaskCatalog Catalog { get; set; } = default!;

    [Inject]
    private AggregateViewRegistry Views { get; set; } = default!;

    [Inject]
    private SerginScopeSession Scopes { get; set; } = default!;

    // Internal, not private: the dialog content does not render in process, so the tests read the title here.
    internal string Heading => Localizer.Text(
        TaskUiTexts.SchedulesIn,
        new
        {
            Task = ActivityRows.CommandLabel(TaskName, Catalog, Views) is { } label ? Localizer.Text(label) : TaskName,
            Scope = TaskUiFormat.ScopeName(Scopes, ScopeId),
        });

    private void Close() => MudDialog.Close();
}

public static class SerginTaskSchedulesDialogExtensions
{
    /// <summary>
    /// Opens the schedules of the collection task <paramref name="taskName"/> in the scope <paramref name="scopeId"/>; the
    /// reference answers once it is open, not when it closes (nothing is read back from it).
    /// </summary>
    public static Task<IDialogReference> ShowTaskSchedulesAsync(this IDialogService dialogs, string schema, string taskName, Guid scopeId)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        DialogParameters<SerginTaskSchedulesDialog> parameters = new()
        {
            { dialog => dialog.Schema, schema },
            { dialog => dialog.TaskName, taskName },
            { dialog => dialog.ScopeId, scopeId },
        };

        return dialogs.ShowAsync<SerginTaskSchedulesDialog>(
            string.Empty, parameters, new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true, CloseOnEscapeKey = true });
    }
}
