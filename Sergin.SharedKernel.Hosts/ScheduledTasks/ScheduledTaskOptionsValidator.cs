using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.ScheduledTasks;

namespace Sergin.SharedKernel.Hosts.ScheduledTasks;

/// <summary>
/// Fails start with <see cref="ScheduledTaskOptions.Validate(out string)"/>'s message, naming exactly which
/// <c>Sergin:ScheduledTasks</c> key is wrong. Mirrors <c>OutboxOptionsValidator</c>.
/// </summary>
internal sealed class ScheduledTaskOptionsValidator : IValidateOptions<ScheduledTaskOptions>
{
    public ValidateOptionsResult Validate(string? name, ScheduledTaskOptions options)
        => options.Validate(out string failure)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failure);
}
