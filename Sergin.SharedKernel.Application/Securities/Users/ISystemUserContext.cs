namespace Sergin.SharedKernel.Application.Securities.Users;

/// <summary>
/// A system identity with no user row, such as the outbox relay. It passes every permission check without a
/// lookup and reads every scoped row. A signed-in user is never one; a user granted <c>AllPlatform</c> is answered
/// by scope like any other grant.
/// </summary>
public interface ISystemUserContext : IUserContext;
