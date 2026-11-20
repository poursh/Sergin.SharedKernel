using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.Tasks;

namespace Sergin.SharedKernel.Hosts.Tasks;

/// <summary>
/// Fails start with <see cref="TaskOptions.Validate(out string)"/>'s message, naming exactly which
/// <c>Sergin:Tasks</c> key is wrong. Mirrors <c>OutboxOptionsValidator</c>.
/// </summary>
internal sealed class TaskOptionsValidator : IValidateOptions<TaskOptions>
{
    public ValidateOptionsResult Validate(string? name, TaskOptions options)
        => options.Validate(out string failure)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failure);
}
