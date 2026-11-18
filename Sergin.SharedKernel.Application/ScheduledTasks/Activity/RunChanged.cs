using Sergin.SharedKernel.Application.ScheduledTasks.Commands;
using Sergin.SharedKernel.Domain.Users;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Activity;

/// <summary>A run of <paramref name="ActingUser"/> changed (started, an item moved, or it finished); the subscriber re-reads it.</summary>
public sealed record RunChanged(BackgroundRunRef Ref, UserId ActingUser);
