using Microsoft.Extensions.Options;

namespace Sergin.SharedKernel.Presentation.Localizations;

/// <summary>
/// Replaces the generic failure text a <c>.Validate(...)</c> predicate would produce with
/// <see cref="SerginLocalizationOptions.Validate(out string)"/>'s message, naming the key. Mirrors
/// <c>SerginTimeZoneOptionsValidator</c>.
/// </summary>
public sealed class SerginLocalizationOptionsValidator : IValidateOptions<SerginLocalizationOptions>
{
    public ValidateOptionsResult Validate(string? name, SerginLocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Validate(out string failure)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failure);
    }
}
