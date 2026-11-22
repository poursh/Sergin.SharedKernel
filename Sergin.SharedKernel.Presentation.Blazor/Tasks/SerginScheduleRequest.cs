using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Domain.Tasks;
using Sergin.SharedKernel.Presentation.Blazor.Aggregates.Model;
using Sergin.SharedKernel.Presentation.Blazor.Aggregates.Runtime;
using Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

namespace Sergin.SharedKernel.Presentation.Blazor.Tasks;

/// <summary>What the Schedule dialog needs to know about the command it schedules; see <see cref="SerginScheduleDialog"/>.</summary>
/// <param name="Schema">The owning module's schema.</param>
/// <param name="CommandTitle">The dialog's title, usually the command's own action name.</param>
/// <param name="BuildTemplate">Builds the command from the arguments the form holds; its target ids are ignored.</param>
/// <param name="Targets">The records the command runs on.</param>
/// <param name="TargetLabels">What shows for each target — data as stored, one per target.</param>
/// <param name="ArgumentsForm">The module's fields for the command's own arguments.</param>
/// <param name="EditingId">A saved schedule to change instead of adding one.</param>
/// <param name="TargetsCaption">
/// The caption above <paramref name="TargetLabels"/>; null for "Records". A collection task, which has no targets, names
/// the scope it runs in under "Scope".
/// </param>
/// <param name="TaskView">
/// A generated view's task: its shown arguments (<see cref="CommandView.TaskFields"/>) render above the trigger, each
/// validated field by field by the task's own validator over <paramref name="BuildTemplate"/>, and Save stays disabled
/// while one is invalid. Null for a module's own page, which passes <paramref name="ArgumentsForm"/> instead.
/// </param>
/// <param name="ArgumentValues">
/// With <paramref name="TaskView"/>, the values its argument fields edit, which <paramref name="BuildTemplate"/> reads: the
/// declared defaults for a new schedule, the saved arguments for an edit (<see cref="SerginScheduleArguments"/>).
/// </param>
public sealed record SerginScheduleRequest(
    string Schema,
    TextDefinition CommandTitle,
    Func<ITaskCommand> BuildTemplate,
    IReadOnlyCollection<TaskScheduleTarget> Targets,
    IReadOnlyList<string> TargetLabels,
    RenderFragment? ArgumentsForm = null,
    TaskScheduleId? EditingId = null,
    TextDefinition? TargetsCaption = null,
    CommandView? TaskView = null,
    GeneratedFormValues? ArgumentValues = null);

public static class SerginScheduleDialogExtensions
{
    /// <summary>Opens the Schedule dialog; true when a schedule was saved (a one-off run, or a cancel, answers false).</summary>
    public static async Task<bool> ShowScheduleAsync(this IDialogService dialogs, SerginScheduleRequest request)
    {
        IDialogReference reference = await dialogs.OpenScheduleAsync(request);

        DialogResult? result = await reference.Result;

        return result is { Canceled: false, Data: true };
    }

    /// <summary>Opens the Schedule dialog without waiting for it to close; internal for the schedule dialog tests.</summary>
    internal static Task<IDialogReference> OpenScheduleAsync(this IDialogService dialogs, SerginScheduleRequest request)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(request);

        DialogParameters<SerginScheduleDialog> parameters = new()
        {
            { dialog => dialog.Schema, request.Schema },
            { dialog => dialog.CommandTitle, request.CommandTitle },
            { dialog => dialog.BuildTemplate, request.BuildTemplate },
            { dialog => dialog.Targets, request.Targets },
            { dialog => dialog.TargetLabels, request.TargetLabels },
            { dialog => dialog.ArgumentsForm, request.ArgumentsForm },
            { dialog => dialog.EditingId, request.EditingId },
            { dialog => dialog.TargetsCaption, request.TargetsCaption },
            { dialog => dialog.TaskView, request.TaskView },
            { dialog => dialog.ArgumentValues, request.ArgumentValues },
        };

        return dialogs.ShowAsync<SerginScheduleDialog>(
            string.Empty,
            parameters,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true });
    }
}

/// <summary>The argument values a generated task's Schedule dialog starts from.</summary>
public static class SerginScheduleArguments
{
    /// <summary>A new schedule's: each shown argument at its declared default, as a start's form begins.</summary>
    public static GeneratedFormValues Defaults(CommandView task)
    {
        ArgumentNullException.ThrowIfNull(task);

        var values = new GeneratedFormValues();
        values.SetDefaults(task.TaskFields);

        return values;
    }

    /// <summary>
    /// A saved schedule's: each shown argument as <paramref name="saved"/> (the command rebuilt from the stored arguments)
    /// holds it, unwrapped to the primitive its input edits.
    /// </summary>
    public static GeneratedFormValues Of(CommandView task, object saved)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(saved);

        var values = new GeneratedFormValues();

        foreach (FormFieldView field in task.TaskFields)
        {
            values.Set(field.Name, ValueObjects.Unwrap(saved.GetType().GetProperty(field.Name)?.GetValue(saved)));
        }

        return values;
    }
}
