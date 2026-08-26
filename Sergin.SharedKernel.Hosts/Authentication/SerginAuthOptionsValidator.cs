using Microsoft.Extensions.Options;

namespace Sergin.SharedKernel.Hosts.Authentication;

/// <summary>
/// Replaces the generic failure text a <c>.Validate(...)</c> predicate on <see cref="OptionsBuilder{TOptions}"/>
/// would produce with <see cref="SerginAuthOptions.Validate(out string)"/>'s precise message, naming exactly
/// which <c>Sergin:Auth</c> key is missing or malformed.
/// </summary>
internal sealed class SerginAuthOptionsValidator : IValidateOptions<SerginAuthOptions>
{
    public ValidateOptionsResult Validate(string? name, SerginAuthOptions options)
        => options.Validate(out string failure)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failure);
}
