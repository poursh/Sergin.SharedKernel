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

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex Kebab();
}
