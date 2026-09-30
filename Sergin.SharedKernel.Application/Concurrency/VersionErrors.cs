namespace Sergin.SharedKernel.Application.Concurrency;

/// <summary>
/// The two version errors. Custom types numbered as their HTTP statuses, so SerginProblemFactory renders the
/// right status with no WebApi special case, and a page recognises a stale version by type.
/// </summary>
public static class VersionErrors
{
    public const int RequiredType = 428;
    public const int StaleType = 412;

    public static Error Required { get; } = Error.Custom(
        RequiredType,
        "General.VersionRequired",
        "This change must say which version of the record it was made against.");

    public static Error Stale { get; } = Error.Custom(
        StaleType,
        "General.VersionStale",
        "The record changed after it was loaded. Reload it and try again.");

    /// <summary>
    /// A versioned send succeeded but published no version: the request touched no versioned aggregate, or a
    /// remote reply carried no version. A caller bug, not a user one, so it is unexpected rather than a custom type.
    /// </summary>
    public static Error NotPublished { get; } = Error.Unexpected(
        "General.VersionNotPublished",
        "The request succeeded but reported no record version.");

    public static bool IsStale(Error error) => (int)error.Type == StaleType;
}
