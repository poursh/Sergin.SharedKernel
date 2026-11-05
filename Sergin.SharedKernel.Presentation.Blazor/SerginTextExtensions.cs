namespace Sergin.SharedKernel.Presentation.Blazor;

/// <summary>How the shared components resolve a text they were given in one of two forms.</summary>
internal static class SerginTextExtensions
{
    /// <summary>
    /// <paramref name="text"/> in the current UI culture when given; otherwise <paramref name="legacy"/>, a plain string
    /// from a module that has not declared its texts yet, shown as written — never looked up, so a name or an id never
    /// reaches the localizer; otherwise <paramref name="fallback"/>, the component's own declared default.
    /// </summary>
    public static string Show(this IStringLocalizer localizer, TextDefinition? text, string? legacy, TextDefinition? fallback = null)
    {
        if (text is not null)
        {
            return localizer.Text(text);
        }

        if (legacy is not null)
        {
            return legacy;
        }

        return fallback is null ? string.Empty : localizer.Text(fallback);
    }
}
