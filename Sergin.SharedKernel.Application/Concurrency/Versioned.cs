namespace Sergin.SharedKernel.Application.Concurrency;

/// <summary>A read model together with the version of the aggregate row it was read from.</summary>
public sealed record Versioned<T>(T Value, RowVersion Version);
