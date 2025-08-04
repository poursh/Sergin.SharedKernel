using Ardalis.GuardClauses;

namespace Sergin.SharedKernel.Domain;

public record RowVersion
{
    private RowVersion(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; private set; }

    public static RowVersion Create()
    {
        return new RowVersion(Guid.CreateVersion7());
    }

    public static RowVersion Create(Guid value)
    {
        Guard.Against.NullOrEmpty(value);

        return new RowVersion(value);
    }

    public static RowVersion Create(string value)
    {
        Guard.Against.NullOrEmpty(value);
        Guard.Against.InvalidStringGuid(value);

        return new RowVersion(Guid.Parse(value));
    }

    public static implicit operator Guid(RowVersion rowVersion) => rowVersion.Value;
    public static implicit operator string(RowVersion rowVersion) => rowVersion.Value.ToString();
    public static implicit operator RowVersion(Guid? value) => value is not null ? Create(value.Value) : Create();
    public static implicit operator RowVersion(string? value) => !string.IsNullOrEmpty(value) ? Create(value) : Create();
}
