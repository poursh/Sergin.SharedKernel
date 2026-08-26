using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Ardalis.GuardClauses;

namespace Sergin.SharedKernel.Application.Securities;

public sealed partial record Permission
{
    public const int MaxLength = 300;
    public const string Format = @"^permission(\.[a-z]+(-[a-z]+)*)+$";
    public const string PermissionPath = $"permission";
    public const int MinParts = 3;

    public static readonly Permission AllPlatform = "permission.sys.platform-all";

    [JsonConstructor]
    private Permission(string value)
    {
        Value = value;
    }

    public string Value { get; private set; }

    public static implicit operator string(Permission permission) => permission.Value;
    public static implicit operator Permission(string? value) => value is not null ? Create(value) : null;

    public static Permission Create(string value)
    {
        Guard.Against.NullOrEmpty(value);

        string v = Kebab().Replace(value, "$1-$2").ToLowerInvariant();

        Guard.Against.InvalidFormat(v, nameof(Permission), Format);
        Guard.Against.StringTooLong(v, MaxLength);

        return new Permission(v);
    }

    /// <summary>
    /// Non-throwing counterpart to <see cref="Create"/>, for values that arrive from outside the code
    /// base — a configuration key, a claim stamped by an earlier release, a row in another module's
    /// table — where an unparseable value is data to reject, not a bug to surface as an exception.
    /// </summary>
    public static bool TryCreate(string? value, [NotNullWhen(true)] out Permission? permission)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            permission = null;
            return false;
        }

        try
        {
            permission = Create(value);
            return true;
        }
        catch (ArgumentException)
        {
            permission = null;
            return false;
        }
    }

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex Kebab();
}
