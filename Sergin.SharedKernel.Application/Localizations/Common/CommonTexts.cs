namespace Sergin.SharedKernel.Application.Localizations.Common;

/// <summary>Texts every module shares: the platform's own verbs (<c>common.actions.*</c>) and a few words.</summary>
public static class CommonTexts
{
    private static readonly TextCategory category = TextCategory.Create("Common");

    private static readonly TextCategory actions = category.Child("Actions");

    public static TextDefinition Create { get; } = actions.Text("Create", "Create");

    public static TextDefinition Add { get; } = actions.Text("Add", "Add");

    public static TextDefinition Edit { get; } = actions.Text("Edit", "Edit");

    public static TextDefinition Rename { get; } = actions.Text("Rename", "Rename");

    public static TextDefinition Remove { get; } = actions.Text("Remove", "Remove");

    /// <summary>What a bulk delete reports after its count: <c>3 deleted</c>.</summary>
    public static TextDefinition Deleted { get; } =
        actions.Text("Deleted", "deleted", "Follows the number of records a bulk delete removed.");

    /// <summary>What a bulk remove reports after its count: <c>3 removed</c>.</summary>
    public static TextDefinition Removed { get; } =
        actions.Text("Removed", "removed", "Follows the number of records a bulk remove took out.");

    public static TextDefinition Save { get; } = actions.Text("Save", "Save");

    public static TextDefinition Cancel { get; } = actions.Text("Cancel", "Cancel");

    public static TextDefinition Delete { get; } = actions.Text("Delete", "Delete");

    public static TextDefinition Reload { get; } = actions.Text("Reload", "Reload");

    public static TextDefinition Refresh { get; } = actions.Text("Refresh", "Refresh");

    public static TextDefinition Open { get; } = actions.Text("Open", "Open");

    public static TextDefinition Close { get; } = actions.Text("Close", "Close");

    public static TextDefinition Apply { get; } = actions.Text("Apply", "Apply");

    public static TextDefinition Reset { get; } = actions.Text("Reset", "Reset");

    public static TextDefinition Clear { get; } = actions.Text("Clear", "Clear");

    public static TextDefinition ClearAll { get; } = actions.Text("ClearAll", "Clear all");

    public static TextDefinition Expand { get; } = actions.Text("Expand", "Expand");

    public static TextDefinition Collapse { get; } = actions.Text("Collapse", "Collapse");

    public static TextDefinition CollapseAll { get; } = actions.Text("CollapseAll", "Collapse all");

    public static TextDefinition CopyLink { get; } = actions.Text("CopyLink", "Copy link");

    public static TextDefinition Help { get; } = actions.Text("Help", "Help");

    public static TextDefinition Information { get; } = actions.Text("Information", "Information");

    public static TextDefinition Language { get; } = category.Text("Language", "Language");

    public static TextDefinition DraftSuffix { get; } = category.Text("DraftSuffix", "draft");
}
