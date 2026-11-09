using Sergin.SharedKernel.Application.Commands.Queries;

namespace Sergin.SharedKernel.Application.Commands.Types;

// The type parameters name the aggregate and child; the registry reads them through reflection.
#pragma warning disable S2326

/// <summary>Acts on every record matching list criteria.</summary>
/// <remarks>
/// Not built yet. Contract: matches resolve through the list query's <c>ListFieldSet</c> and <c>ListQuerySql</c> (the
/// same meaning as the list the user saw); scope and soft-delete filters apply; permission is checked per matched
/// record's scope; refused when the match count differs from <see cref="ExpectedCount"/>; one save, all or nothing;
/// no per-item version.
/// </remarks>
public interface ICriteriaCommand<TRoot, TListQuery> : ICollectionTarget<TRoot>
    where TRoot : class, IAggregateRoot
    where TListQuery : ListQuery
{
    Term? Term { get; }

    Filtering? Filtering { get; }

    int ExpectedCount { get; }
}

/// <summary>Creates N roots from items.</summary>
/// <remarks>Not built yet. Contract: creation, N items; each item validated as one Create would be.</remarks>
public interface IImportCommand<TRoot, TItem> : ICollectionTarget<TRoot>
    where TRoot : class, IAggregateRoot
{
    IReadOnlyList<TItem> Items { get; }
}

/// <summary>Creates a root from a source root. Its response implements <see cref="ICreatedResponse"/>.</summary>
/// <remarks>Not built yet. Contract: creation from a source; the source is not version-checked.</remarks>
public interface ICloneCommand<TRoot, TId> : IEntityTarget<TRoot, TId>
    where TRoot : class, IAggregateRoot<TId>
    where TId : notnull;

/// <summary>Restores a soft-deleted root.</summary>
/// <remarks>
/// Not built yet. Contract: the root must be <c>SoftDeletable()</c> (host-start guard); the target is a deleted row,
/// so the soft-delete filter is bypassed for the lookup.
/// </remarks>
public interface IRestoreCommand<TRoot, TId> : IEntityTarget<TRoot, TId>
    where TRoot : class, IAggregateRoot<TId>
    where TId : notnull;

/// <summary>Permanently removes a soft-deleted root.</summary>
/// <remarks>Not built yet. Contract: as <see cref="IRestoreCommand{TRoot, TId}"/>.</remarks>
public interface IPurgeCommand<TRoot, TId> : IEntityTarget<TRoot, TId>
    where TRoot : class, IAggregateRoot<TId>
    where TId : notnull;

/// <summary>Moves a child to a position among its siblings.</summary>
/// <remarks>Not built yet.</remarks>
public interface IReorderCommand<TRoot, TRootId, TChild, TChildId> : IChildEntityTarget<TRoot, TRootId, TChild, TChildId>
    where TRoot : class, IAggregateRoot<TRootId>
    where TRootId : notnull
    where TChild : class, IEntity<TChildId>
    where TChildId : notnull
{
    int Position { get; }
}

/// <summary>Exports the records matching list criteria, without paging.</summary>
/// <remarks>Not built yet.</remarks>
public interface IExportQuery<TRoot, TListQuery> : ICollectionTarget<TRoot>
    where TRoot : class, IAggregateRoot
    where TListQuery : ListQuery
{
    Term? Term { get; }

    Filtering? Filtering { get; }
}

#pragma warning restore S2326
