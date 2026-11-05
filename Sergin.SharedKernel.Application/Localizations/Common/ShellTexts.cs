using Sergin.SharedKernel.Application.Securities.Users;

namespace Sergin.SharedKernel.Application.Localizations.Common;

/// <summary>
/// The Blazor shell's own words — layout, navigation, page types, lists, trees, dialogs and the welcome page — under
/// <c>common.&lt;part&gt;.*</c>. The verbs its buttons carry are <see cref="CommonTexts"/>'.
/// </summary>
public static class ShellTexts
{
    private static readonly TextCategory common = TextCategory.Create("Common");

    /// <summary>The app bar and the drawer.</summary>
    public static class Layout
    {
        private static readonly TextCategory category = common.Child("Layout");

        public static TextDefinition SignOut { get; } = category.Text("SignOut", "Sign out");

        public static TextDefinition SwitchToLightMode { get; } = category.Text("SwitchToLightMode", "Switch to light mode");

        public static TextDefinition SwitchToDarkMode { get; } = category.Text("SwitchToDarkMode", "Switch to dark mode");
    }

    /// <summary>The home entry, the page header and the action menus.</summary>
    public static class Navigation
    {
        private static readonly TextCategory category = common.Child("Navigation");

        public static TextDefinition Home { get; } = category.Text("Home", "Home");

        public static TextDefinition PageActions { get; } = category.Text("PageActions", "Page actions");

        public static TextDefinition Actions { get; } = category.Text("Actions", "Actions");
    }

    /// <summary>The default home slot.</summary>
    public static class Welcome
    {
        private static readonly TextCategory category = common.Child("Welcome");

        public static TextDefinition Prompt { get; } = category.Text("Prompt", "Choose a section from the menu to get started.");
    }

    /// <summary>The page types' frame: the overview and the header's link button.</summary>
    public static class Pages
    {
        private static readonly TextCategory category = common.Child("Pages");

        public static TextDefinition Overview { get; } = category.Text("Overview", "Overview");

        public static TextDefinition LinkCopied { get; } = category.Text("LinkCopied", "Link copied");

        public static TextDefinition CopyRefused { get; } = category.Text("CopyRefused", "The browser did not allow copying the link.");
    }

    /// <summary>The aggregate and entity lists: search, filters, empty states, selection.</summary>
    public static class Lists
    {
        private static readonly TextCategory category = common.Child("Lists");

        public static TextDefinition QuickSearch { get; } = category.Text("QuickSearch", "Quick search");

        public static TextDefinition QuickSearchPlaceholder { get; } = category.Text("QuickSearchPlaceholder", "Quick search…");

        public static TextDefinition AdvancedFilter { get; } = category.Text("AdvancedFilter", "Advanced filter");

        public static TextDefinition FilteredBy { get; } = category.Text("FilteredBy", "Filtered by");

        public static TextDefinition Search { get; } = category.Text("Search", "Search");

        public static TextDefinition NothingHereYet { get; } = category.Text("NothingHereYet", "Nothing here yet.");

        public static TextDefinition NothingMatchesFilters { get; } = category.Text("NothingMatchesFilters", "Nothing matches these filters.");

        public static TextDefinition NothingMatchesSearch { get; } = category.Text("NothingMatchesSearch", "Nothing matches this search.");

        public static TextDefinition Selection { get; } = category.Text("Selection", "Selection");

        public static TextDefinition Selected { get; } =
            category.Text("Selected", "selected", "Follows the number of selected rows, which is shown in bold before it.");
    }

    /// <summary>The information dialog: scope, version and audit of one record.</summary>
    public static class Information
    {
        private static readonly TextCategory category = common.Child("Information");

        public static TextDefinition Scope { get; } = category.Text("Scope", "Scope");

        public static TextDefinition Global { get; } = category.Text("Global", "Global");

        public static TextDefinition NoScope { get; } = category.Text("NoScope", "This record belongs to no scope.");

        public static TextDefinition Version { get; } = category.Text("Version", "Version");

        public static TextDefinition VersionHint { get; } = category.Text("VersionHint", "Edits and deletes are checked against this version.");

        public static TextDefinition Audit { get; } = category.Text("Audit", "Audit");

        public static TextDefinition Created { get; } = category.Text("Created", "Created");

        public static TextDefinition Modified { get; } = category.Text("Modified", "Modified");

        public static TextDefinition By { get; } = category.Text("By", "by", "Between a time and the name of who made the change.");

        public static TextDefinition Never { get; } = category.Text("Never", "Never");

        public static TextDefinition SystemActorName { get; } = category.Text("System", SystemActor.DisplayName);
    }

    /// <summary>The scope picker in the app bar and its dialog.</summary>
    public static class Scopes
    {
        private static readonly TextCategory category = common.Child("Scopes");

        public static TextDefinition Scope { get; } = category.Text("Scope", "Scope");

        public static TextDefinition NoScope { get; } = category.Text("NoScope", "No scope");

        public static TextDefinition NoScopeToWorkIn { get; } = category.Text("NoScopeToWorkIn", "You have no scope to work in.");

        public static TextDefinition SearchScopes { get; } = category.Text("SearchScopes", "Search scopes");

        public static TextDefinition NoScopeMatches { get; } = category.Text("NoScopeMatches", "No scope matches.");
    }

    /// <summary>The tree layout: its rail, panel, filter and node menu.</summary>
    public static class Trees
    {
        private static readonly TextCategory category = common.Child("Trees");

        public static TextDefinition ShowTree { get; } = category.Text("ShowTree", "Show tree");

        public static TextDefinition HideTree { get; } = category.Text("HideTree", "Hide tree");

        public static TextDefinition ResizeTree { get; } = category.Text("ResizeTree", "Resize tree");

        public static TextDefinition FilterPlaceholder { get; } = category.Text("FilterPlaceholder", "Filter tree…");

        public static TextDefinition OneMatch { get; } = category.Text("OneMatch", "1 match");

        public static TextDefinition Matches { get; } = category.Text("Matches", "{Count} matches");

        public static TextDefinition DeleteConfirm { get; } =
            category.Text("DeleteConfirm", "Delete {Name}? It disappears from every list and page.");
    }
}
