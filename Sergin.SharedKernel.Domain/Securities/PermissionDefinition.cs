namespace Sergin.SharedKernel.Domain.Securities;

/// <summary>
/// A permission a module declares: its <see cref="Permission"/> name plus the human label the
/// role editor shows. The name is validated by <see cref="Permission.Create"/>; this type is the
/// only place a permission name is written as a string literal.
/// </summary>
public sealed class PermissionDefinition : IEquatable<PermissionDefinition>
{
    private PermissionDefinition(Permission permission, string label)
    {
        Permission = permission;
        Label = label;
        Schema = permission.Value.Split('.')[1];
    }

    /// <summary>The platform-wide permission, reserved to the <c>sys</c> schema.</summary>
    public static PermissionDefinition AllPlatform { get; } = new(Permission.AllPlatform, "Everything, everywhere");

    public Permission Permission { get; }

    public string Label { get; }

    /// <summary>The module schema the permission belongs to: the second segment of its name.</summary>
    public string Schema { get; }

    public static PermissionDefinition Create(string name, string label)
    {
        Permission permission;

        try
        {
            permission = Permission.Create(name);
        }
        catch (ArgumentException exception)
        {
            // Name the value: this runs in a static initializer, where the caller sees only the failing class.
            throw new ArgumentException($"'{name}' is not a valid permission name: {exception.Message}", nameof(name), exception);
        }

        Guard.Against.NullOrWhiteSpace(label);

        return new PermissionDefinition(permission, label);
    }

    public bool Equals(PermissionDefinition? other) =>
        other is not null && Permission.Value == other.Permission.Value;

    public override bool Equals(object? obj) => Equals(obj as PermissionDefinition);

    public override int GetHashCode() => Permission.Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Permission.Value;
}
