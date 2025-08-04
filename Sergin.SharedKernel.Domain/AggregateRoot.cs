namespace Sergin.SharedKernel.Domain;

public interface IAggregateRoot : IEntity
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void Raise(IDomainEvent domainEvent);
    void ClearDomainEvents();
}

public interface IAggregateRoot<out TId> : IEntity<TId>, IAggregateRoot
    where TId : notnull;

public abstract class AggregateRoot : IAggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot()
    {
    }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => [.. _domainEvents];

    public abstract object?[] GetKeys();

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    public void Raise(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }
}


public abstract class AggregateRoot<TId> : AggregateRoot, IAggregateRoot<TId>
    where TId : notnull
{
    protected AggregateRoot()
    {
    }

    public TId Id { get; protected init; }

    public override object?[] GetKeys() => [Id];
}
