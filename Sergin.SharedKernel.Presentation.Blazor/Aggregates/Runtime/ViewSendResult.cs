using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// What a generated view's send answered, with the response untyped: the response record, the version a versioned
/// send read or wrote, or the errors.
/// </summary>
/// <param name="Value">The response, or null on failure.</param>
/// <param name="Version">The version of a versioned send, or null.</param>
/// <param name="Errors">The errors; empty on success.</param>
public sealed record ViewSendResult(object? Value, RowVersion? Version, IReadOnlyList<Error> Errors)
{
    public bool IsError => Errors.Count > 0;
}
