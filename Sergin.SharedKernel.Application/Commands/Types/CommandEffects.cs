namespace Sergin.SharedKernel.Application.Commands.Types;

// The type parameters name the aggregate and child; the registry reads them through reflection.
#pragma warning disable S2326

/// <summary>Creates a root in the collection (optionally also <see cref="IUnderParent{TParent, TParentId}"/>). Its response implements <see cref="ICreatedResponse"/>.</summary>
public interface ICreateCommand<TRoot, TId> : ICollectionTarget<TRoot>
    where TRoot : class, IAggregateRoot<TId>
    where TId : notnull;

/// <summary>Adds a child to a root. Its response implements <see cref="ICreatedResponse"/>.</summary>
public interface IAddChildCommand<TRoot, TRootId, TChild, TChildId> : IRootTarget<TRoot, TRootId>
    where TRoot : class, IAggregateRoot<TRootId>
    where TRootId : notnull
    where TChild : class, IEntity<TChildId>
    where TChildId : notnull;

/// <summary>Edits several fields of an entity at once (a form).</summary>
public interface IUpdateCommand<TRoot, TId> : IEntityTarget<TRoot, TId>
    where TRoot : class, IAggregateRoot<TId>
    where TId : notnull;

/// <summary>Edits several fields of a child entity at once.</summary>
public interface IUpdateCommand<TRoot, TRootId, TChild, TChildId> : IChildEntityTarget<TRoot, TRootId, TChild, TChildId>
    where TRoot : class, IAggregateRoot<TRootId>
    where TRootId : notnull
    where TChild : class, IEntity<TChildId>
    where TChildId : notnull;

/// <summary>Sets exactly one shown property besides the target ids; that property is the value.</summary>
public interface ISetPropertyCommand<TRoot, TId> : IEntityTarget<TRoot, TId>
    where TRoot : class, IAggregateRoot<TId>
    where TId : notnull;

/// <summary>Sets exactly one shown property of a child entity besides the target ids.</summary>
public interface ISetPropertyCommand<TRoot, TRootId, TChild, TChildId> : IChildEntityTarget<TRoot, TRootId, TChild, TChildId>
    where TRoot : class, IAggregateRoot<TRootId>
    where TRootId : notnull
    where TChild : class, IEntity<TChildId>
    where TChildId : notnull;

/// <summary>A state change with no shown property besides the ids. A pair declares <see cref="IReverseOf{TOther}"/> on one side.</summary>
public interface ITransitionCommand<TRoot, TId> : IEntityTarget<TRoot, TId>
    where TRoot : class, IAggregateRoot<TId>
    where TId : notnull;

/// <summary>A state change of a child entity with no shown property besides the ids.</summary>
public interface ITransitionCommand<TRoot, TRootId, TChild, TChildId> : IChildEntityTarget<TRoot, TRootId, TChild, TChildId>
    where TRoot : class, IAggregateRoot<TRootId>
    where TRootId : notnull
    where TChild : class, IEntity<TChildId>
    where TChildId : notnull;

/// <summary>Moves a root within its own tree.</summary>
public interface IMoveCommand<TRoot, TId> : IEntityTarget<TRoot, TId>
    where TRoot : class, IAggregateRoot<TId>
    where TId : notnull
{
    TId NewParentId { get; }
}

/// <summary>Deletes a root. Extra shown properties (such as <c>Cascade</c>) are allowed and asked for in the confirm.</summary>
public interface IDeleteCommand<TRoot, TId> : IEntityTarget<TRoot, TId>
    where TRoot : class, IAggregateRoot<TId>
    where TId : notnull;

/// <summary>Removes a child from its root.</summary>
public interface IRemoveChildCommand<TRoot, TRootId, TChild, TChildId> : IChildEntityTarget<TRoot, TRootId, TChild, TChildId>
    where TRoot : class, IAggregateRoot<TRootId>
    where TRootId : notnull
    where TChild : class, IEntity<TChildId>
    where TChildId : notnull;

/// <summary>Adds a value (not an entity) to the root's own collection; the value is the one shown non-id property of type <typeparamref name="TValue"/>.</summary>
public interface IAddValueCommand<TRoot, TId, TValue> : IEntityTarget<TRoot, TId>
    where TRoot : class, IAggregateRoot<TId>
    where TId : notnull;

/// <summary>Removes a value from the root's own collection; the value is the one shown non-id property of type <typeparamref name="TValue"/>.</summary>
public interface IRemoveValueCommand<TRoot, TId, TValue> : IEntityTarget<TRoot, TId>
    where TRoot : class, IAggregateRoot<TId>
    where TId : notnull;

/// <summary>No UI and no target rule (for example provisioning an external user).</summary>
public interface ISystemCommand;

/// <summary>The command may run once per selected row; allowed only on entity and child targets.</summary>
public interface ISelectable;

/// <summary>This command undoes <typeparamref name="TOther"/>; declared on one side of a transition pair.</summary>
public interface IReverseOf<TOther>
    where TOther : IBaseCommand;

/// <summary>A creation response that carries the new record's id, so a UI can navigate to it.</summary>
public interface ICreatedResponse
{
    Guid Id { get; }
}

#pragma warning restore S2326
