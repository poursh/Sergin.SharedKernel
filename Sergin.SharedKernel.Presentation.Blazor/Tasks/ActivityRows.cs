using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Application.Tasks;
using Sergin.SharedKernel.Application.Tasks.Activity;
using Sergin.SharedKernel.Application.Tasks.Commands;
using Sergin.SharedKernel.Modules;
using Sergin.SharedKernel.Presentation.Blazor.Modules;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Tasks;

/// <summary>Where a run of the activity indicator stands.</summary>
internal enum ActivityState
{
    Running,
    Succeeded,
    PartlyFailed,
    Failed,
    Cancelled,
}

/// <summary>
/// One run as the activity indicator shows it: the command's view label (null when no view names it, and
/// <paramref name="CommandName"/> shows instead), the target's stored label or a record count, the target's detail link
/// for a one-record root target, where the run stands, a one-record run's first error as stored, its target id and
/// progress (0–100, null until its command reports), the caption of the module the run belongs to (null when no UI
/// module has its schema), its count of cancelled items (zero until it completes), whether a person may cancel it and, for a
/// collection task's run, its scope. <paramref name="Done"/> is the
/// items no longer waiting while the run is running (a run's counts are written only when it completes, so a cancelled
/// item counts as done for the bar), and its succeeded and failed items once it finished.
/// </summary>
internal sealed record ActivityRow(
    TaskRunRef Ref,
    TextDefinition? CommandLabel,
    string CommandName,
    string Target,
    string? Href,
    ActivityState State,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    int Done,
    int Total,
    string? FirstErrorCode,
    string? FirstErrorDescription,
    Guid? TargetId,
    int? Progress,
    TextDefinition? ModuleCaption,
    int Cancelled,
    bool Cancelable = true,
    Guid? ScopeId = null)
{
    public bool IsRunning => State == ActivityState.Running;
}

/// <summary>Turns the activity read into rows: pure, so it is tested without a browser.</summary>
internal static class ActivityRows
{
    /// <param name="items">The runs read.</param>
    /// <param name="catalog">The task catalogue.</param>
    /// <param name="views">The view registry.</param>
    /// <param name="recordsText">What names a run with no stored label by its count.</param>
    /// <param name="moduleCaption">What names a schema's module.</param>
    /// <param name="scopeName">
    /// The name of a scope the circuit lists, or null: what names a collection task's run, which has no record. A scope
    /// not listed is named by its short id.
    /// </param>
    public static IReadOnlyList<ActivityRow> Build(
        IReadOnlyList<ActivityItem> items,
        TaskCatalog catalog,
        AggregateViewRegistry views,
        Func<int, string> recordsText,
        Func<string, TextDefinition?> moduleCaption,
        Func<Guid, string?>? scopeName = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(views);
        ArgumentNullException.ThrowIfNull(recordsText);
        ArgumentNullException.ThrowIfNull(moduleCaption);

        return [.. items.Select(item => Row(item, catalog, views, recordsText, moduleCaption, scopeName))];
    }

    /// <summary>
    /// What names a schema's module beside a run: the UI module's caption, or else its menu group's title (a module alone
    /// in its group declares no caption); null for a schema no UI module has.
    /// </summary>
    public static Func<string, TextDefinition?> ModuleCaption(SerginUiModuleCatalog modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        Dictionary<string, TextDefinition> captions = [];

        foreach (ISerginWebUiModule module in modules.Modules)
        {
            captions.TryAdd(module.Schema, module.NavCaption ?? module.NavGroup.Title);
        }

        return schema => captions.GetValueOrDefault(schema);
    }

    /// <summary>"1 record" or "{Count} records", in the current culture: what a run with no stored label is named by.</summary>
    public static Func<int, string> RecordsText(IStringLocalizer localizer, ICultureFormatter formatter)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(formatter);

        return count => count == 1
            ? localizer.Text(TaskUiTexts.Activity.OneRecord)
            : localizer.Text(TaskUiTexts.Activity.Records, new { Count = formatter.Number(count) });
    }

    public static ActivityState StateOf(ActivityItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Unfinished > 0)
        {
            return ActivityState.Running;
        }

        // A run cancelled part way through, with no failure, reads as cancelled: the finished message counts what was done.
        if (item.Failed == 0)
        {
            return item.Cancelled > 0 ? ActivityState.Cancelled : ActivityState.Succeeded;
        }

        return item.Succeeded == 0 ? ActivityState.Failed : ActivityState.PartlyFailed;
    }

    /// <summary>
    /// The view label a scheduled command is shown by (the label of the view command whose request it is, on the root or
    /// one of its children); null when the catalogue does not know the name or no view names the command.
    /// </summary>
    public static TextDefinition? CommandLabel(string commandName, TaskCatalog catalog, AggregateViewRegistry views) =>
        CommandViewOf(commandName, catalog, views)?.Label;

    /// <summary>
    /// The icon of the view command a scheduled command is shown by; null when the catalogue does not know the name, no view
    /// names the command, or the view declares no icon for it.
    /// </summary>
    public static string? CommandIcon(string commandName, TaskCatalog catalog, AggregateViewRegistry views) =>
        CommandViewOf(commandName, catalog, views)?.Icon;

    private static CommandView? CommandViewOf(string commandName, TaskCatalog catalog, AggregateViewRegistry views)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(views);

        TaskDescriptor? command = catalog.ByName(commandName);

        return ViewOf(command, command is null ? null : views.Find(command.Root));
    }

    private static ActivityRow Row(
        ActivityItem item,
        TaskCatalog catalog,
        AggregateViewRegistry views,
        Func<int, string> recordsText,
        Func<string, TextDefinition?> moduleCaption,
        Func<Guid, string?>? scopeName)
    {
        TaskDescriptor? command = catalog.ByName(item.CommandName);
        AggregateViewModel? model = command is null ? null : views.Find(command.Root);

        return new ActivityRow(
            item.Ref,
            LabelOf(command, model),
            item.CommandName,
            TargetOf(item, recordsText, scopeName),
            HrefOf(item, command, model),
            StateOf(item),
            item.StartedAtUtc,
            item.CompletedAtUtc,
            DoneOf(item),
            item.Total,
            item.FirstErrorCode,
            item.FirstErrorDescription,
            item.Total == 1 ? item.TargetId : null,
            item.Total == 1 ? item.Progress : null,
            moduleCaption(item.Ref.Schema),
            item.Cancelled,
            item.Cancelable,
            item.ScopeId);
    }

    // A record's stored label; else, for a collection task's run, the scope it runs in (a name is user data, as stored);
    // else a record count.
    private static string TargetOf(ActivityItem item, Func<int, string> recordsText, Func<Guid, string?>? scopeName)
    {
        if (item.TargetLabel is { } label)
        {
            return label;
        }

        if (item.ScopeId is { } scope)
        {
            return scopeName?.Invoke(scope) ?? scope.ToString("N")[..8];
        }

        return recordsText(item.Total);
    }

    // The run's succeeded and failed counts are written only when it completes: while it runs, done is what no longer waits.
    private static int DoneOf(ActivityItem item) =>
        item.Unfinished > 0 ? item.Total - item.Unfinished : item.Succeeded + item.Failed;

    private static TextDefinition? LabelOf(TaskDescriptor? command, AggregateViewModel? model) => ViewOf(command, model)?.Label;

    private static CommandView? ViewOf(TaskDescriptor? command, AggregateViewModel? model)
    {
        if (command is null || model is null)
        {
            return null;
        }

        return model.Commands.FirstOrDefault(view => view.Request == command.RequestType)
            ?? model.Children.SelectMany(child => child.Commands).FirstOrDefault(view => view.Request == command.RequestType);
    }

    // A link only for one record of the root itself: a child target's page needs its root, and many targets have no one page.
    private static string? HrefOf(ActivityItem item, TaskDescriptor? command, AggregateViewModel? model) =>
        item.Total == 1 && item.TargetId is { } target && command?.TargetKind == CommandTargetKind.Entity && model is not null
            ? model.Routes.Detail?.Invoke(target)
            : null;
}
