namespace Sergin.SharedKernel.Application.Commands.Types;

// The type parameters name the aggregate (and child); the registry reads them through reflection, never a member.
#pragma warning disable S2326

/// <summary>Acts on one existing aggregate root, named by its id.</summary>
public interface IEntityTarget<TRoot, out TId>
    where TRoot : class, IAggregateRoot<TId>
    where TId : notnull
{
    TId Id { get; }
}

/// <summary>Acts on one existing child entity of a root, named by the root's id and the child's id.</summary>
public interface IChildEntityTarget<TRoot, out TRootId, TChild, out TChildId>
    where TRoot : class, IAggregateRoot<TRootId>
    where TRootId : notnull
    where TChild : class, IEntity<TChildId>
    where TChildId : notnull
{
    TRootId RootId { get; }

    TChildId Id { get; }
}

/// <summary>Acts on a root's child collection; the child does not exist yet (add-child).</summary>
public interface IRootTarget<TRoot, out TRootId>
    where TRoot : class, IAggregateRoot<TRootId>
    where TRootId : notnull
{
    TRootId RootId { get; }
}

/// <summary>Acts on the aggregate's collection in the current scope (creation, criteria, collection queries).</summary>
public interface ICollectionTarget<TRoot>
    where TRoot : class, IAggregateRoot;

/// <summary>
/// The new or listed roots sit under a structural parent, the same aggregate or another. A parent id is a
/// reference, not a target: it carries no version rule.
/// </summary>
public interface IUnderParent<TParent, out TParentId>
    where TParent : class, IAggregateRoot<TParentId>
    where TParentId : notnull
{
    TParentId ParentId { get; }
}

#pragma warning restore S2326
