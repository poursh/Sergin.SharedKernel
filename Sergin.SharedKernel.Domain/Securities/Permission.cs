using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Sergin.SharedKernel.Domain.Securities;

/// <summary>
/// A permission name, <c>permission.&lt;schema&gt;.&lt;resource&gt;.&lt;action&gt;</c>. The format rules are
/// <see cref="SerginStringKey"/>'s; a permission adds the <c>permission</c> first segment and a schema after it.
/// </summary>
public sealed record Permission : SerginStringKey
{
    public const int MaxLength = 300;
    public const string PermissionPath = "permission";
    public const int MinParts = 3;

    // Declared before AllPlatform: static fields initialize in order, and AllPlatform's Create reads this one.
    private static readonly SerginStringKeyRules rules = new(PermissionPath, MinParts, MaxLength);

    public static readonly Permission AllPlatform = "permission.sys.platform-all";

    [JsonConstructor]
    private Permission(string value)
        : base(value)
    {
    }

    public static implicit operator Permission(string? value) => value is not null ? Create(value) : null;

    public static Permission Create(string value) => new(Normalize(value, rules, nameof(Permission)));

    /// <inheritdoc cref="SerginStringKey.TryCreate{TKey}"/>
    public static bool TryCreate(string? value, [NotNullWhen(true)] out Permission? permission) =>
        TryCreate(value, Create, out permission);
}
