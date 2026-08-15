using Microsoft.Extensions.Options;

namespace Sergin.SharedKernel.Hosts.WebUi.Users;

/// <summary>
/// Replaces the generic failure text a <c>.Validate(...)</c> predicate on <see cref="OptionsBuilder{TOptions}"/>
/// would produce with <see cref="DevUserOptions.Validate(out string)"/>'s precise message, naming exactly which
/// <c>Sergin:DevUser</c> key is wrong.
/// </summary>
internal sealed class DevUserOptionsValidator : IValidateOptions<DevUserOptions>
{
    public ValidateOptionsResult Validate(string? name, DevUserOptions options)
        => options.Validate(out string failure)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failure);
}
