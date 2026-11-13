namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

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
}
