namespace Sergin.SharedKernel.Application.Localizations;

/// <summary>One declared text as the catalog knows it: where it came from and which category it sits under.</summary>
public sealed record TextCatalogEntry(TextDefinition Definition, string Module, IReadOnlyList<string> Category, Type DeclaringType);
