using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Domain.Securities;
using Sergin.SharedKernel.Domain.Users;

namespace Sergin.SharedKernel.Hosts.Outbox;

/// <summary>
/// The identity every relayed message is consumed under. It holds <see cref="Permission.AllPlatform"/>, so
/// <see cref="IUserContext.IsSystemAdmin"/> is true and a consumer that dispatches a
/// <c>[RequiredPermissions]</c> command passes <c>PermissionCheckPipelineBehavior</c> — a background scope
/// has no signed-in user to borrow a permission set from, and a consumer that could not act would make
/// every integration event a dead letter. The id is a fixed, recognisable value rather than a fresh one per
/// host start, so anything that audits by user id sees one stable actor.
/// </summary>
internal sealed record RelayUserContext : IUserContext
{
    public UserId Id { get; } = new(Guid.Parse("01920000-0000-7000-8000-00000000000f"));

    public string UserName => "outbox-relay";

    public string FirstName => "Outbox";

    public string LastName => "Relay";

    public string Email => "outbox-relay@sergin.local";

    public HashSet<Permission> Permissions { get; } = [Permission.AllPlatform];
}
