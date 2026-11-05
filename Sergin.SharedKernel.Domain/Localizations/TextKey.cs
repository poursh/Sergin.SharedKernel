namespace Sergin.SharedKernel.Domain.Localizations;

/// <summary>
/// The key of one piece of UI or error text, <c>&lt;module&gt;.&lt;path…&gt;.&lt;name&gt;</c>, e.g.
/// <c>device-management.devices.new</c>. The first segment is the owning module; the format rules are
/// <see cref="SerginStringKey"/>'s.
/// </summary>
public sealed record TextKey : SerginStringKey
{
    public const int MaxLength = 300;
    public const int MinSegments = 2;

    private static readonly SerginStringKeyRules rules = new(FirstSegment: null, MinSegments, MaxLength);

    private TextKey(string value)
        : base(value)
    {
    }

    /// <summary>The module segment, the first one.</summary>
    public string Module => Segments[0];

    public static TextKey Create(string value) => new(Normalize(value, rules, nameof(TextKey)));
}
