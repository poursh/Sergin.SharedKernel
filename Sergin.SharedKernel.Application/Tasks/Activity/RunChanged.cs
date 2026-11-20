using Sergin.SharedKernel.Application.Tasks.Commands;

namespace Sergin.SharedKernel.Application.Tasks.Activity;

/// <summary>A run of <paramref name="ActingUser"/> changed (started, an item moved, or it finished); the subscriber re-reads it.</summary>
public sealed record RunChanged(TaskRunRef Ref, UserId ActingUser);
