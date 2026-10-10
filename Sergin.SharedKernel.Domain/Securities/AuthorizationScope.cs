namespace Sergin.SharedKernel.Domain.Securities;

/// <summary>
/// Well-known values of the scope tree that SharedKernel needs without referencing the module that owns it.
/// </summary>
public static class AuthorizationScope
{
    /// <summary>
    /// The single root of the scope tree, seeded by UserAccess's <c>AddScopedRbac</c> migration. A grant at the root
    /// over the whole tree is a global grant.
    /// </summary>
    public static Guid RootId { get; } = Guid.Parse("01920000-0000-7000-8000-0000000000b1");
}
