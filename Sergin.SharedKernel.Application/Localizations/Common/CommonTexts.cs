namespace Sergin.SharedKernel.Application.Localizations.Common;

/// <summary>Texts every module shares: the platform's own verbs (<c>common.actions.*</c>) and a few words.</summary>
public static class CommonTexts
{
    private static readonly TextCategory category = TextCategory.Create("Common");

    private static readonly TextCategory actions = category.Child("Actions");

    private static readonly TextCategory bulk = category.Child("Bulk");

    private static readonly TextCategory permissionGroups = category.Child("Permissions").Child("Groups");

    public static TextDefinition Create { get; } = actions.Text("Create", "Create");

    public static TextDefinition Add { get; } = actions.Text("Add", "Add");

    public static TextDefinition Edit { get; } = actions.Text("Edit", "Edit");

    public static TextDefinition Rename { get; } = actions.Text("Rename", "Rename");

    public static TextDefinition Remove { get; } = actions.Text("Remove", "Remove");

    /// <summary>What a bulk delete reports: <c>3 deleted</c>.</summary>
    public static TextDefinition BulkDeleted { get; } =
        bulk.Text("Deleted", "{Count} deleted", "Reported after a bulk delete; {Count} is the number of records it removed.");

    /// <summary>What a bulk remove reports: <c>3 removed</c>.</summary>
    public static TextDefinition BulkRemoved { get; } =
        bulk.Text("Removed", "{Count} removed", "Reported after a bulk remove; {Count} is the number of records it took out.");

    /// <summary>What a bulk run reports for the items a stop left unsent: <c>2 not sent</c>.</summary>
    public static TextDefinition BulkNotSent { get; } =
        bulk.Text("NotSent", "{Count} not sent", "Reported when a bulk run stopped early; {Count} is the number of records it never sent.");

    /// <summary>The permission picker's heading over the platform's own permission.</summary>
    public static TextDefinition PermissionGroupPlatform { get; } =
        permissionGroups.Text("Platform", "Platform", "Heads the platform's own permission in the permission pickers.");

    /// <summary>The permission picker's heading over the permissions of an aggregate that declares no name.</summary>
    public static TextDefinition PermissionGroupOther { get; } =
        permissionGroups.Text("Other", "Other", "Heads, in the permission pickers, the permissions of an aggregate that declares no name.");

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
