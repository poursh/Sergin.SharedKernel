namespace Sergin.SharedKernel.Application.Commands.Types;

// The type parameters name the aggregate; the registry reads them through reflection.
#pragma warning disable S2326

/// <summary>
/// A task: a command that always runs in the background as a run item, never inline, and answers no response (its
/// outcome is the run item's status). The non-generic marker is for guards and generic constraints; a request
/// implements one of the two generic shapes, never this alone.
/// </summary>
public interface ITaskCommand : IBaseCommand;

/// <summary>
/// A task on one root entity, named by its <c>Id</c>. With <see cref="ISelectable"/> it may run on several picked
/// records, one run item each. It never takes an expected version: a run executes later than the page read the record.
/// </summary>
public interface ITaskCommand<TRoot, TId> : ICommand, IEntityTarget<TRoot, TId>, ITaskCommand
    where TRoot : class, IAggregateRoot<TId>
    where TId : notnull;

/// <summary>
/// A task on the aggregate's collection in the run's scope, with no record target: one run item per run (a
/// whole-collection job). The root gives the texts, the permissions' scope rule and the placement in the views.
/// </summary>
public interface ITaskCommand<TRoot> : ICommand, ICollectionTarget<TRoot>, ITaskCommand
    where TRoot : class, IAggregateRoot;

#pragma warning restore S2326
