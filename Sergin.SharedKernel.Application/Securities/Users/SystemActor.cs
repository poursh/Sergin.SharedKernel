namespace Sergin.SharedKernel.Application.Securities.Users;

/// <summary>
/// The fixed user id the platform itself acts under: the outbox relay's identity, and the actor migrations stamp
/// when they backfill audit columns or seed rows. It has no row in any user store, so
/// <see cref="IUserDirectory"/> never names it; a display shows <see cref="DisplayName"/> instead.
/// </summary>
public static class SystemActor
{
    public static readonly Guid Id = Guid.Parse("01920000-0000-7000-8000-00000000000f");

    public const string DisplayName = "System";
}
