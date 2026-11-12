using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.ScheduledTasks;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Domain.ScheduledTasks;
using Sergin.SharedKernel.Domain.Securities;
using Sergin.SharedKernel.Presentation.Blazor.Pages;

namespace Sergin.SharedKernel.Presentation.Blazor.ScheduledTasks;

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
    IReadOnlyCollection<ScheduledTaskTarget> Targets,
    IReadOnlyList<string> TargetLabels,
    RenderFragment? ArgumentsForm = null,
    ScheduledTaskId? EditingId = null);

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

/// <summary>
/// The Schedule… action a view puts next to a schedulable command: one on a page's actions bar
/// (<see cref="SerginPageAction"/>) and one on a list's selection bar (<see cref="SerginBulkAction{TItem}"/>, for a command
/// that runs once per selected row).
/// </summary>
/// <remarks>
/// A person needs two permissions: the platform's scheduling permission (<see cref="ScheduledTaskPermissions.Manage"/>) and
/// the command's own. The factory checks the first and returns nothing without it; the action carries the second, which the
/// actions bar hides it for. Presentation only — the handlers check both again.
/// </remarks>
public static class SerginScheduleAction
{
    /// <summary>The page action, or null when the user may not manage scheduled tasks on the current scope.</summary>
    /// <param name="scopeAuthorization">The caller's own authorization.</param>
    /// <param name="onClick">What a click runs; create it with <c>EventCallback.Factory.Create(this, …)</c>.</param>
    /// <param name="commandPermission">The permission the scheduled command itself needs.</param>
    /// <param name="anyScope">Whether the command's permission is checked on any scope, for an aggregate that is not scoped.</param>
    public static async Task<SerginPageAction?> PageAsync(
        IScopeAuthorization scopeAuthorization, EventCallback onClick, PermissionDefinition commandPermission, bool anyScope = false)
    {
        ArgumentNullException.ThrowIfNull(scopeAuthorization);
        ArgumentNullException.ThrowIfNull(commandPermission);

        return await scopeAuthorization.CurrentUserHasAsync(ScheduledTaskPermissions.Manage)
            ? new SerginPageAction(
                ScheduledTaskUiTexts.ScheduleAction,
                Icons.Material.Outlined.Schedule,
                SerginActionKind.Secondary,
                OnClick: onClick,
                Permission: commandPermission,
                AnyScope: anyScope)
            : null;
    }

    /// <summary>The selection-bar action, or null when the user may not manage scheduled tasks on the current scope.</summary>
    public static async Task<SerginBulkAction<TItem>?> BulkAsync<TItem>(
        IScopeAuthorization scopeAuthorization,
        Func<IReadOnlyList<TItem>, Task> onRun,
        PermissionDefinition commandPermission,
        bool anyScope = false)
    {
        ArgumentNullException.ThrowIfNull(scopeAuthorization);
        ArgumentNullException.ThrowIfNull(onRun);
        ArgumentNullException.ThrowIfNull(commandPermission);

        return await scopeAuthorization.CurrentUserHasAsync(ScheduledTaskPermissions.Manage)
            ? new SerginBulkAction<TItem>(
                ScheduledTaskUiTexts.ScheduleAction,
                Icons.Material.Outlined.Schedule,
                SerginActionKind.Secondary,
                onRun,
                Permission: commandPermission,
                AnyScope: anyScope)
            : null;
    }
}
