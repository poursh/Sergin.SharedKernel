namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Model;

/// <summary>What a command does to its root, read from its effect interface; it decides the action's default look.</summary>
public enum CommandRole
{
    Create,
    Update,
    Delete,
    Transition,
    SetProperty,
    Move,

    /// <summary>Adds a child entity to the root (<c>IAddChildCommand</c>).</summary>
    AddChild,

    /// <summary>Removes a child entity from the root (<c>IRemoveChildCommand</c>).</summary>
    RemoveChild,

    /// <summary>
    /// A task over one record (<c>ITaskCommand&lt;TRoot, TId&gt;</c>): a detail and row action that starts it as a run over
    /// the record, or over the selected rows when it is <c>ISelectable</c>.
    /// </summary>
    Task,

    /// <summary>
    /// A task over the root's collection in the current scope (<c>ITaskCommand&lt;TRoot&gt;</c>): a list header action that
    /// starts it as a run with no record.
    /// </summary>
    CollectionTask,
}
