using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Outbox;

namespace Sergin.SharedKernel.Hosts.Outbox;

/// <summary>
/// Replaces the generic failure text a <c>.Validate(...)</c> predicate on <see cref="OptionsBuilder{TOptions}"/>
/// would produce with <see cref="OutboxOptions.Validate(out string)"/>'s precise message, naming exactly which
/// <c>Sergin:Outbox</c> key is wrong. Mirrors <c>DevUserOptionsValidator</c>.
/// </summary>
internal sealed class OutboxOptionsValidator : IValidateOptions<OutboxOptions>
{
    public ValidateOptionsResult Validate(string? name, OutboxOptions options)
        => options.Validate(out string failure)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failure);
}
