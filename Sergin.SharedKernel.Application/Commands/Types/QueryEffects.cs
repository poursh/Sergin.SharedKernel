namespace Sergin.SharedKernel.Application.Commands.Types;

// The type parameters name the aggregate and child; the registry reads them through reflection.
#pragma warning disable S2326

/// <summary>Reads one root by id.</summary>
public interface IGetOneQuery<TRoot, TId> : IEntityTarget<TRoot, TId>
    where TRoot : class, IAggregateRoot<TId>
    where TId : notnull;

/// <summary>Reads one child entity of a root.</summary>
public interface IChildGetOneQuery<TRoot, TRootId, TChild, TChildId> : IChildEntityTarget<TRoot, TRootId, TChild, TChildId>
    where TRoot : class, IAggregateRoot<TRootId>
    where TRootId : notnull
    where TChild : class, IEntity<TChildId>
    where TChildId : notnull;

/// <summary>Reads a root's audit record.</summary>
public interface IAuditQuery<TRoot, TId> : IEntityTarget<TRoot, TId>
    where TRoot : class, IAggregateRoot<TId>
    where TId : notnull;

/// <summary>Reads the overview figures of one root.</summary>
public interface IEntityOverviewQuery<TRoot, TId> : IEntityTarget<TRoot, TId>
    where TRoot : class, IAggregateRoot<TId>
    where TId : notnull;

/// <summary>Reads a derived view of one root (progress, effective permissions).</summary>
public interface IEntityViewQuery<TRoot, TId> : IEntityTarget<TRoot, TId>
    where TRoot : class, IAggregateRoot<TId>
    where TId : notnull;

/// <summary>Lists the aggregate's roots in the current scope (optionally also under a parent).</summary>
public interface IListOf<TRoot> : ICollectionTarget<TRoot>
    where TRoot : class, IAggregateRoot;

/// <summary>Reads the aggregate's tree.</summary>
public interface ITreeQuery<TRoot> : ICollectionTarget<TRoot>
    where TRoot : class, IAggregateRoot;

/// <summary>Reads the overview figures of the aggregate's collection.</summary>
public interface IOverviewQuery<TRoot> : ICollectionTarget<TRoot>
    where TRoot : class, IAggregateRoot;

/// <summary>Lists the children of one root.</summary>
public interface IChildListOf<TRoot, TRootId, TChild> : IRootTarget<TRoot, TRootId>
    where TRoot : class, IAggregateRoot<TRootId>
    where TRootId : notnull
    where TChild : class, IEntity;

#pragma warning restore S2326
