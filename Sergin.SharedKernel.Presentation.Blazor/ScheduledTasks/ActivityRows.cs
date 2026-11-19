using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Application.ScheduledTasks;
using Sergin.SharedKernel.Application.ScheduledTasks.Activity;
using Sergin.SharedKernel.Application.ScheduledTasks.Commands;
using Sergin.SharedKernel.Modules;
using Sergin.SharedKernel.Presentation.Blazor.Modules;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.ScheduledTasks;

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
/// progress (0–100, null until its command reports), and the caption of the module the run belongs to (null when no UI
/// module has its schema).
/// </summary>
internal sealed record ActivityRow(
    BackgroundRunRef Ref,
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
    TextDefinition? ModuleCaption)
{
    public bool IsRunning => State == ActivityState.Running;
}

/// <summary>Turns the activity read into rows: pure, so it is tested without a browser.</summary>
internal static class ActivityRows
{
    public static IReadOnlyList<ActivityRow> Build(
        IReadOnlyList<ActivityItem> items,
        ScheduledCommandCatalog catalog,
        AggregateViewRegistry views,
        Func<int, string> recordsText,
        Func<string, TextDefinition?> moduleCaption)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(views);
        ArgumentNullException.ThrowIfNull(recordsText);
        ArgumentNullException.ThrowIfNull(moduleCaption);

        return [.. items.Select(item => Row(item, catalog, views, recordsText, moduleCaption))];
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
            ? localizer.Text(ScheduledTaskUiTexts.Activity.OneRecord)
            : localizer.Text(ScheduledTaskUiTexts.Activity.Records, new { Count = formatter.Number(count) });
    }

    public static ActivityState StateOf(ActivityItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Unfinished > 0)
        {
            return ActivityState.Running;
        }

        if (item.Cancelled > 0 && item.Succeeded == 0 && item.Failed == 0)
        {
            return ActivityState.Cancelled;
        }

        // Cancelled items beside succeeded ones leave the run partly done, as a failure beside them would.
        if (item.Failed == 0 && item.Cancelled == 0)
        {
            return ActivityState.Succeeded;
        }

        return item.Succeeded == 0 ? ActivityState.Failed : ActivityState.PartlyFailed;
    }

    private static ActivityRow Row(
        ActivityItem item,
        ScheduledCommandCatalog catalog,
        AggregateViewRegistry views,
        Func<int, string> recordsText,
        Func<string, TextDefinition?> moduleCaption)
    {
        ScheduledCommand? command = catalog.TryGet(item.CommandName, out ScheduledCommand? found) ? found : null;
        AggregateViewModel? model = command is null ? null : views.Find(command.Root);

        return new ActivityRow(
            item.Ref,
            LabelOf(command, model),
            item.CommandName,
            item.TargetLabel ?? recordsText(item.Total),
            HrefOf(item, command, model),
            StateOf(item),
            item.StartedAtUtc,
            item.CompletedAtUtc,
            item.Succeeded + item.Failed,
            item.Total,
            item.FirstErrorCode,
            item.FirstErrorDescription,
            item.Total == 1 ? item.TargetId : null,
            item.Total == 1 ? item.Progress : null,
            moduleCaption(item.Ref.Schema));
    }

    private static TextDefinition? LabelOf(ScheduledCommand? command, AggregateViewModel? model)
    {
        if (command is null || model is null)
        {
            return null;
        }

        return model.Commands.FirstOrDefault(view => view.Request == command.RequestType)?.Label
            ?? model.Children.SelectMany(child => child.Commands).FirstOrDefault(view => view.Request == command.RequestType)?.Label;
    }

    // A link only for one record of the root itself: a child target's page needs its root, and many targets have no one page.
    private static string? HrefOf(ActivityItem item, ScheduledCommand? command, AggregateViewModel? model) =>
        item.Total == 1 && item.TargetId is { } target && command?.TargetKind == CommandTargetKind.Entity && model is not null
            ? model.Routes.Detail?.Invoke(target)
            : null;
}
