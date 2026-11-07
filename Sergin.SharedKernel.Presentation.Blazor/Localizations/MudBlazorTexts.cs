using System.Collections.Concurrent;
using System.Globalization;
using System.Resources;
using MudBlazor;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Domain.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Localizations;

/// <summary>
/// MudBlazor's own texts (pager captions, data-grid filter labels, date-picker captions) as catalog definitions
/// under <c>common.mud-blazor.</c>, with MudBlazor's English, read from its resource set, as the default.
/// </summary>
/// <remarks>
/// A MudBlazor key <c>MudDataGrid_RowsPerPage</c> becomes <c>common.mud-blazor.mud-data-grid.rows-per-page</c>: each
/// <c>_</c>-separated part is one kebab-case segment. MudBlazor's positional <c>{0}</c> becomes the named
/// <c>{Value}</c> (<c>{1}</c> becomes <c>{Value1}</c>), because placeholders here are named.
/// </remarks>
public static class MudBlazorTexts
{
    private const string ResourceBaseName = "MudBlazor.Resources.LanguageResource";
    private const string KeyPrefix = "common.mud-blazor.";

    private static readonly ConcurrentDictionary<string, string> catalogKeys = new(StringComparer.Ordinal);

    /// <summary>The source the catalog reads; module segment <c>common</c>.</summary>
    public static TextSource Source { get; } = TextSource.FromDefinitions("common", Build(), typeof(MudBlazorTexts), TextOrigin.Ui);

    /// <summary>The catalog key of a MudBlazor resource key.</summary>
    public static string CatalogKeyOf(string mudKey)
    {
        ArgumentNullException.ThrowIfNull(mudKey);

        return catalogKeys.GetOrAdd(
            mudKey,
            static key => KeyPrefix + string.Join(SerginStringKey.Separator, key.Split('_').Select(SerginStringKey.ToSegment)));
    }

    /// <summary>MudBlazor's positional arguments as the named ones its catalog defaults use.</summary>
    public static IReadOnlyDictionary<string, object?> NamedArguments(IReadOnlyList<object> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        Dictionary<string, object?> named = new(StringComparer.Ordinal);

        for (int index = 0; index < arguments.Count; index++)
        {
            named[index == 0 ? "Value" : $"Value{index}"] = arguments[index];
        }

        return named;
    }

    private static List<TextDefinition> Build()
    {
        ResourceManager resources = new(ResourceBaseName, typeof(MudLocalizer).Assembly);
        ResourceSet english = resources.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false)
            ?? throw new InvalidOperationException($"MudBlazor's English resource set '{ResourceBaseName}' was not found.");

        List<TextDefinition> definitions = [];

        foreach (System.Collections.DictionaryEntry entry in english)
        {
            if (entry is { Key: string key, Value: string text })
            {
                definitions.Add(new TextDefinition(TextKey.Create(CatalogKeyOf(key)), ToNamed(text), $"MudBlazor's own text '{key}'."));
            }
        }

        return [.. definitions.OrderBy(definition => definition.Key.Value, StringComparer.Ordinal)];
    }

    private static string ToNamed(string text)
    {
        string named = text;

        for (int index = 9; index >= 0; index--)
        {
            named = named.Replace($"{{{index}}}", index == 0 ? "{Value}" : $"{{Value{index}}}", StringComparison.Ordinal);
        }

        return named;
    }
}
