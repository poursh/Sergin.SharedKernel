namespace Sergin.SharedKernel.Application.Localizations;

/// <summary>
/// One declared text as the catalog knows it: where it came from, which category it sits under, and whether its source
/// is the application or the UI.
/// </summary>
public sealed record TextCatalogEntry(
    TextDefinition Definition, string Module, IReadOnlyList<string> Category, Type DeclaringType, TextOrigin Origin);
