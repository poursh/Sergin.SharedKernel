namespace Sergin.SharedKernel.Application.Localizations.Common;

/// <summary>Texts every module shares: the platform's own verbs.</summary>
public static class CommonTexts
{
    private static readonly TextCategory category = TextCategory.Create("Common");

    public static TextDefinition Save { get; } = category.Text("Save", "Save");

    public static TextDefinition Cancel { get; } = category.Text("Cancel", "Cancel");

    public static TextDefinition Delete { get; } = category.Text("Delete", "Delete");

    public static TextDefinition Reload { get; } = category.Text("Reload", "Reload");

    public static TextDefinition Language { get; } = category.Text("Language", "Language");

    public static TextDefinition DraftSuffix { get; } = category.Text("DraftSuffix", "draft");
}
