namespace Sergin.SharedKernel.Presentation;

/// <summary>
/// The running application's display identity, bound from the <c>Sergin</c> configuration section.
/// </summary>
/// <remarks>
/// This lives in the presentation-agnostic project rather than in a host bootstrap so every front end can
/// name the application the same way — the Blazor shell's app bar and browser tab today, an OpenAPI
/// document title if a Web API host is added back.
/// </remarks>
public sealed class SerginApplicationOptions
{
    /// <summary>The configuration key, relative to the <c>Sergin</c> section.</summary>
    public const string ApplicationNameKey = "ApplicationName";

    /// <summary>
    /// Title shown in the app bar and the browser tab. Omitting <c>Sergin:ApplicationName</c> keeps this
    /// default; setting it to a blank value fails startup instead of silently rendering an empty title.
    /// </summary>
    public string ApplicationName { get; set; } = "Sergin Application";

    /// <summary>
    /// Validates the bound values, producing a message that names the offending key rather than the generic
    /// text an <c>OptionsBuilder.Validate(predicate)</c> lambda would emit.
    /// </summary>
    public bool Validate(out string failure)
    {
        if (string.IsNullOrWhiteSpace(ApplicationName))
        {
            failure = $"Sergin:{ApplicationNameKey} must not be blank. "
                + "Remove the key entirely to accept the default application name.";

            return false;
        }

        failure = string.Empty;

        return true;
    }
}
