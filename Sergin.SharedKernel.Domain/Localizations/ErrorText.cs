namespace Sergin.SharedKernel.Domain.Localizations;

/// <summary>
/// Builds the metadata an <c>Error</c> carries so a presentation layer can render it from a declared text key
/// instead of the error code.
/// </summary>
public static class ErrorText
{
    public const string MetadataKey = "sergin:text";
    public const string ArgumentsKey = "sergin:text-args";

    /// <summary>
    /// Metadata naming <paramref name="key"/> (checked through <see cref="TextKey.Create"/>) and, when given, the
    /// named <paramref name="arguments"/> its text is formatted with.
    /// </summary>
    public static Dictionary<string, object> Of(string key, IReadOnlyDictionary<string, object?>? arguments = null)
    {
        Dictionary<string, object> metadata = new()
        {
            [MetadataKey] = TextKey.Create(key).Value,
        };

        if (arguments is not null)
        {
            metadata[ArgumentsKey] = arguments;
        }

        return metadata;
    }
}
