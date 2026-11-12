using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Presentation.Blazor.Pages;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

/// <summary>
/// The actions bar of a generated record view, from its root's commands: an update links to the edit route, a
/// transition and a delete run through the view's own handlers, each disabled while it runs. Commands are offered in
/// the model's order, a hidden one not at all (the actions bar puts the danger ones at the far end); each carries its
/// first required permission, which the bar hides it for.
/// </summary>
/// <param name="receiver">The view, so a click re-renders it.</param>
/// <param name="runAsync">Sends a transition.</param>
/// <param name="deleteAsync">Confirms and sends a delete.</param>
internal sealed class GeneratedCommandActions(IHandleEvent receiver, Func<CommandView, Task> runAsync, Func<CommandView, Task> deleteAsync)
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

    /// <param name="model">The root's model.</param>
    /// <param name="id">The record's id.</param>
    /// <param name="menuItemsOf">The entries behind a command's drop-down; empty for a plain button.</param>
    public IReadOnlyList<SerginPageAction> For(AggregateViewModel model, Guid id, Func<CommandView, IReadOnlyList<SerginMenuItem>> menuItemsOf)
    {
        List<SerginPageAction> actions = [];

        foreach (CommandView command in model.Commands.Where(command => !command.Hidden))
        {
            SerginPageAction? action = command.Role switch
            {
                CommandRole.Update when model.Routes.Edit is { } edit => Action(command, menuItemsOf) with { Href = edit(id) },
                CommandRole.Transition => Action(command, menuItemsOf) with
                {
                    OnClick = EventCallback.Factory.Create(receiver, () => runAsync(command)),
                },
                CommandRole.Delete => Action(command, menuItemsOf) with
                {
                    OnClick = EventCallback.Factory.Create(receiver, () => deleteAsync(command)),
                },
                _ => null,
            };

            if (action is not null)
            {
                actions.Add(action);
            }
        }

        return actions;
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
