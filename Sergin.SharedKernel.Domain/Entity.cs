namespace Sergin.SharedKernel.Domain;

public interface IEntity 
{
    object?[] GetKeys();
}

public interface IEntity<out TId> : IEntity
    where TId : notnull
{
    TId Id { get; }
}

public abstract class Entity : IEntity
{
    protected Entity()
    {
    }

    public abstract object?[] GetKeys();
}

public abstract class Entity<TId> : Entity, IEntity<TId>
    where TId : notnull
{
    protected Entity()
    {
    
    }

    public TId Id { get; protected init; }

    public override object?[] GetKeys() => [Id];
}
