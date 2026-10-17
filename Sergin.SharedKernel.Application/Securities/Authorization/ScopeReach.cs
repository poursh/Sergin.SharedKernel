namespace Sergin.SharedKernel.Application.Securities.Authorization;

/// <summary>
/// Where a user holds one permission: the scopes their grants reach, as the read filter needs them. An entry that
/// covers its subtree reaches every scope at or below <see cref="ScopeReachEntry.Path"/>; one that does not reaches
/// its own scope only. <see cref="Everything"/> is a system identity's reach, <see cref="Nothing"/> a denial's.
/// </summary>
public sealed class ScopeReach
{
    private ScopeReach(bool unrestricted, IReadOnlyCollection<ScopeReachEntry> entries)
    {
        Unrestricted = unrestricted;
        Entries = entries;
    }

    public static ScopeReach Everything { get; } = new(unrestricted: true, []);

    public static ScopeReach Nothing { get; } = new(unrestricted: false, []);

    public bool Unrestricted { get; }

    public IReadOnlyCollection<ScopeReachEntry> Entries { get; }

    public static ScopeReach Of(IEnumerable<ScopeReachEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return new ScopeReach(unrestricted: false, [.. entries.Distinct()]);
    }

    /// <summary>
    /// The scopes both reaches cover, for a request that needs two permissions on the same record. Two subtrees
    /// meet in the deeper one when one holds the other; a single scope survives when the other side covers it.
    /// </summary>
    public ScopeReach Intersect(ScopeReach other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Unrestricted)
        {
            return other;
        }

        if (other.Unrestricted)
        {
            return this;
        }

        return Of(Entries.SelectMany(left => other.Entries.Select(right => Meet(left, right))).OfType<ScopeReachEntry>());
    }

    private static ScopeReachEntry? Meet(ScopeReachEntry left, ScopeReachEntry right)
    {
        if (left.Covers(right))
        {
            return right;
        }

        return right.Covers(left) ? left : null;
    }
}

/// <summary>One grant's reach: <paramref name="Path"/> is <paramref name="ScopeId"/>'s <c>ltree</c> path.</summary>
public sealed record ScopeReachEntry(Guid ScopeId, string Path, bool IncludeSubtree)
{
    /// <summary>Whether every scope <paramref name="other"/> reaches is reached by this entry too.</summary>
    public bool Covers(ScopeReachEntry other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (!IncludeSubtree)
        {
            return !other.IncludeSubtree && other.ScopeId == ScopeId;
        }

        return other.Path == Path
            || other.Path.Length > Path.Length
                && other.Path[Path.Length] == '.'
                && other.Path.StartsWith(Path, StringComparison.Ordinal);
    }
}
