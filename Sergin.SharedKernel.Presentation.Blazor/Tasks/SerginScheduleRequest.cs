using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Domain.Tasks;

namespace Sergin.SharedKernel.Presentation.Blazor.Tasks;

/// <summary>What the Schedule dialog needs to know about the command it schedules; see <see cref="SerginScheduleDialog"/>.</summary>
/// <param name="Schema">The owning module's schema.</param>
/// <param name="CommandTitle">The dialog's title, usually the command's own action name.</param>
/// <param name="BuildTemplate">Builds the command from the arguments the form holds; its target ids are ignored.</param>
/// <param name="Targets">The records the command runs on.</param>
/// <param name="TargetLabels">What shows for each target — data as stored, one per target.</param>
/// <param name="ArgumentsForm">The module's fields for the command's own arguments.</param>
/// <param name="EditingId">A saved schedule to change instead of adding one.</param>
public sealed record SerginScheduleRequest(
    string Schema,
    TextDefinition CommandTitle,
    Func<IBaseCommand> BuildTemplate,
    IReadOnlyCollection<TaskScheduleTarget> Targets,
    IReadOnlyList<string> TargetLabels,
    RenderFragment? ArgumentsForm = null,
    TaskScheduleId? EditingId = null);

public static class SerginScheduleDialogExtensions
{
    /// <summary>Opens the Schedule dialog; true when a schedule was saved (a one-off run, or a cancel, answers false).</summary>
    public static async Task<bool> ShowScheduleAsync(this IDialogService dialogs, SerginScheduleRequest request)
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
        };

        IDialogReference reference = await dialogs.ShowAsync<SerginScheduleDialog>(
            string.Empty,
            parameters,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true });

        DialogResult? result = await reference.Result;

        return result is { Canceled: false, Data: true };
    }
}
