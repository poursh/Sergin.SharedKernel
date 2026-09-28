using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Presentation;

namespace Sergin.SharedKernel.Hosts;

/// <summary>
/// Replaces the generic failure text a <c>.Validate(...)</c> predicate on <see cref="OptionsBuilder{TOptions}"/>
/// would produce with <see cref="SerginTimeZoneOptions.Validate(out string)"/>'s precise message, naming
/// exactly which <c>Sergin</c> key is wrong. Mirrors <c>SerginApplicationOptionsValidator</c>.
/// </summary>
internal sealed class SerginTimeZoneOptionsValidator : IValidateOptions<SerginTimeZoneOptions>
{
    public ValidateOptionsResult Validate(string? name, SerginTimeZoneOptions options)
        => options.Validate(out string failure)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failure);
}
