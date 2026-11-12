using Sergin.SharedKernel.Application.Securities.Users;

namespace Sergin.SharedKernel.Application.ScheduledTasks;

/// <summary>
/// The identity a scheduled run item sends as: the user the run acts for, marked as a scheduled run. Deliberately
/// not an <see cref="ISystemUserContext"/>, so every permission check looks up that user's stored grants exactly as
/// for a signed-in request: a run is never more powerful than the person it acts for, and a revoked grant or a
/// deactivated user fails the next run with Forbidden.
/// </summary>
public interface IDelegatedUserContext : IUserContext
{
    /// <summary>The run this send belongs to.</summary>
    Guid RunId { get; }
}
