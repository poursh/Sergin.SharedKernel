using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Presentation;

namespace Sergin.SharedKernel.Hosts.WebUi;

/// <summary>
/// Replaces the generic failure text a <c>.Validate(...)</c> predicate on <see cref="OptionsBuilder{TOptions}"/>
/// would produce with <see cref="SerginApplicationOptions.Validate(out string)"/>'s precise message, naming
/// exactly which <c>Sergin</c> key is wrong. Mirrors <c>DevUserOptionsValidator</c>.
/// </summary>
internal sealed class SerginApplicationOptionsValidator : IValidateOptions<SerginApplicationOptions>
{
    public ValidateOptionsResult Validate(string? name, SerginApplicationOptions options)
        => options.Validate(out string failure)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failure);
}
