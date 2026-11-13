using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Presentation.Blazor.Pages;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

/// <summary>Where a view's declared links go among its commands' actions.</summary>
internal enum ViewLinkPlacement
{
    /// <summary>No links.</summary>
    None,

    /// <summary>After the commands that are not Danger, before the Danger ones (the detail bar).</summary>
    AfterCommands,

    /// <summary>Before every command (a list row's icons).</summary>
    BeforeCommands,
}

/// <summary>
/// The actions of a generated record view (or of one list row), from its root's commands: an update links to the edit route, a
/// transition and a delete run through the view's own handlers, and a set-property command in the Dialog edit mode opens
/// its dialog when the view gave an edit handler (a detail view does, a list row does not); each is disabled while it runs. Commands are offered in
/// the model's order, a hidden one not at all (the actions bar puts the danger ones at the far end); each carries its
/// first required permission, which the bar hides it for. A command of an <c>IReverseOf</c> pair is offered only for a record
/// its <c>AppliesWhen</c> accepts. The view's declared links are Secondary actions that carry no permission.
/// </summary>
/// <param name="receiver">The view, so a click re-renders it.</param>
/// <param name="runAsync">Sends a transition.</param>
/// <param name="deleteAsync">Confirms and sends a delete.</param>
/// <param name="editAsync">Opens a Dialog-mode set-property command's dialog; null where the view offers none.</param>
internal sealed class GeneratedCommandActions(
    IHandleEvent receiver, Func<CommandView, Task> runAsync, Func<CommandView, Task> deleteAsync, Func<CommandView, Task>? editAsync = null)
{
    private readonly HashSet<Type> running = [];

    /// <summary>Runs <paramref name="work"/> for <paramref name="command"/> with its button disabled meanwhile.</summary>
    public async Task<T> WhileRunningAsync<T>(CommandView command, Func<Task<T>> work)
    {
        running.Add(command.Request);

        try
        {
            return await work();
        }
        finally
        {
            running.Remove(command.Request);
        }
    }

    /// <summary>Whether <paramref name="command"/> is running, so its control is disabled.</summary>
    public bool IsRunning(CommandView command) => running.Contains(command.Request);

    /// <param name="model">The root's model.</param>
    /// <param name="id">The record's id.</param>
    /// <param name="menuItemsOf">The entries behind a command's drop-down; empty for a plain button.</param>
    /// <param name="roles">Only the commands of these roles (a list row's icons, or its menu); null for every role.</param>
    /// <param name="run">Sends a transition on this record, replacing the handler the actions were made with (a list row's).</param>
    /// <param name="delete">Confirms and sends a delete on this record, replacing the handler the actions were made with.</param>
    /// <param name="record">The loaded record or list row, which a command's <c>AppliesWhen</c> reads; null offers none of those commands.</param>
    /// <param name="links">Whether and where the model's links are offered.</param>
    public IReadOnlyList<SerginPageAction> For(
        AggregateViewModel model,
        Guid id,
        Func<CommandView, IReadOnlyList<SerginMenuItem>> menuItemsOf,
        IReadOnlySet<CommandRole>? roles = null,
        Func<CommandView, Task>? run = null,
        Func<CommandView, Task>? delete = null,
        object? record = null,
        ViewLinkPlacement links = ViewLinkPlacement.None)
    {
        ArgumentNullException.ThrowIfNull(model);

        List<SerginPageAction> actions = [];
        Func<CommandView, Task> runCommand = run ?? runAsync;
        Func<CommandView, Task> deleteCommand = delete ?? deleteAsync;

        foreach (CommandView command in model.Commands.Where(command => !command.Hidden && (roles is null || roles.Contains(command.Role))))
        {
            if (command.AppliesWhen is { } applies && (record is null || !applies(record)))
            {
                continue;
            }

            SerginPageAction? action = command.Role switch
            {
                CommandRole.Update when model.Routes.Edit is { } edit => Action(command, menuItemsOf) with { Href = edit(id) },
                CommandRole.Transition => Action(command, menuItemsOf) with
                {
                    OnClick = EventCallback.Factory.Create(receiver, () => runCommand(command)),
                },
                CommandRole.Delete => Action(command, menuItemsOf) with
                {
                    OnClick = EventCallback.Factory.Create(receiver, () => deleteCommand(command)),
                },
                CommandRole.SetProperty when editAsync is not null && command is { EditMode: SerginEditMode.Dialog, ValueField: not null } =>
                    Action(command, menuItemsOf) with
                    {
                        OnClick = EventCallback.Factory.Create(receiver, () => editAsync(command)),
                    },
                _ => null,
            };

            if (action is not null)
            {
                actions.Add(action);
            }
        }

        return WithLinks(model, id, actions, links);
    }

    private static List<SerginPageAction> WithLinks(AggregateViewModel model, Guid id, List<SerginPageAction> actions, ViewLinkPlacement placement)
    {
        if (placement == ViewLinkPlacement.None || model.Links is not { Count: > 0 } declared)
        {
            return actions;
        }

        SerginPageAction[] links = [.. declared.Select(link => new SerginPageAction(link.Label, link.Icon, SerginActionKind.Secondary, Href: link.Href(id)))];

        return placement == ViewLinkPlacement.BeforeCommands
            ? [.. links, .. actions]
            : [.. actions.Where(action => action.Kind != SerginActionKind.Danger), .. links, .. actions.Where(action => action.Kind == SerginActionKind.Danger)];
    }

    private SerginPageAction Action(CommandView command, Func<CommandView, IReadOnlyList<SerginMenuItem>> menuItemsOf)
    {
        IReadOnlyList<SerginMenuItem> items = menuItemsOf(command);

        return new SerginPageAction(
            command.Label,
            command.Icon,
            command.Kind,
            Permission: command.Permissions.FirstOrDefault(),
            AnyScope: command.AnyScope,
            Disabled: running.Contains(command.Request),
            MenuItems: items.Count > 0 ? items : null);
    }
}
