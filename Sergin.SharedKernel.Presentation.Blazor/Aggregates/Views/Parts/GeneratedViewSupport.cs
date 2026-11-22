using System.Reflection;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Application.Tasks.Commands;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Modules;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Modules;
using Sergin.SharedKernel.Presentation.Blazor.Navigation;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

/// <summary>One list column: the list field, the list field id it sorts by (or null), and whether it links to the record.</summary>
internal sealed record ListColumn(FieldView Field, string? SortId, bool Links);

/// <summary>What the three generated views and the list body share, written once.</summary>
internal static class GeneratedViewSupport
{
    /// <summary>
    /// The list section step: the nav entry naming the list route, else a step titled as the list; null when the root
    /// has no list route.
    /// </summary>
    public static SerginBreadcrumb? ListStep(AggregateViewModel model, SerginUiModuleCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(catalog);

        if (model.Routes.List is not { } list)
        {
            return null;
        }

        SerginNavItem? item = catalog.NavItems.FirstOrDefault(candidate => string.Equals(candidate.Href, list, StringComparison.OrdinalIgnoreCase));

        return item is null ? new SerginBreadcrumb(model.Title, list) : SerginBreadcrumb.Of(item);
    }

    /// <summary>
    /// The trail steps of a parent (<c>BelongsTo</c>): <paramref name="parent"/>'s list step, then, when
    /// <paramref name="parentId"/> is known, the parent linking to its detail, named <paramref name="parentName"/> as stored
    /// or, without a name, by the parent's singular.
    /// </summary>
    public static IReadOnlyList<SerginBreadcrumb> ParentSteps(AggregateViewModel parent, SerginUiModuleCatalog catalog, Guid? parentId, string? parentName)
    {
        ArgumentNullException.ThrowIfNull(parent);

        List<SerginBreadcrumb> steps = [];

        if (ListStep(parent, catalog) is { } list)
        {
            steps.Add(list);
        }

        if (parentId is { } id)
        {
            string? href = parent.Routes.Detail?.Invoke(id);
            steps.Add(parentName is null ? new SerginBreadcrumb(parent.Singular, href) : SerginBreadcrumb.Data(parentName, href));
        }

        return steps;
    }

    /// <summary>Where a record that belongs to a parent leaves to: the parent's detail, else the parent's list, else the home page.</summary>
    public static string ParentLeaveHref(AggregateViewModel parent, Guid? parentId)
    {
        ArgumentNullException.ThrowIfNull(parent);

        return (parentId is { } id ? parent.Routes.Detail?.Invoke(id) : null) ?? parent.Routes.List ?? "/";
    }

    /// <summary>
    /// Sends an overview query once and hands back its response, or null when the read failed: an overview is a summary, so a
    /// failure leaves the figures empty rather than a second error beside the page's own.
    /// </summary>
    public static async Task<object?> LoadOverviewAsync(ISerginDispatcher dispatcher, RequestInvoker invoker, object request)
    {
        ArgumentNullException.ThrowIfNull(invoker);

        ViewSendResult result = await invoker.SendAsync(dispatcher, request);

        return result.IsError ? null : result.Value;
    }

    /// <summary>The compiled reader of a list item's <c>Id</c> (a value object read through to its Guid), or null when it has none.</summary>
    public static Func<object, object?>? IdReaderOf(Type item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance) is { } id ? ValueObjects.UnwrappedGetter(id) : null;
    }

    /// <summary>A list item's Guid id, read by <paramref name="reader"/> (from <see cref="IdReaderOf"/>); refuses an item with none.</summary>
    public static Guid IdOf(Func<object, object?>? reader, object item) =>
        reader?.Invoke(item) is Guid id
            ? id
            : throw new InvalidOperationException($"{item.GetType().FullName} has no Guid Id, so a generated list cannot link or act on its rows.");

    /// <summary>The list field id a field sorts by: the one named after its property that the list field set declares sortable, else null.</summary>
    public static string? SortIdOf(ListFieldSet? fieldSet, FieldView field)
    {
        ArgumentNullException.ThrowIfNull(field);

        return fieldSet?.Fields
            .FirstOrDefault(candidate => candidate.Sortable && string.Equals(candidate.Id, field.Name, StringComparison.OrdinalIgnoreCase))
            ?.Id;
    }

    /// <summary>A column per visible list field, sortable as the field set declares, the display field linking.</summary>
    public static IReadOnlyList<ListColumn> ColumnsOf(IReadOnlyList<FieldView> fields, ListFieldSet? fieldSet, FieldView? titleField)
    {
        ArgumentNullException.ThrowIfNull(fields);

        return [.. fields.Where(field => !field.Hidden).Select(field => new ListColumn(field, SortIdOf(fieldSet, field), field == titleField))];
    }

    /// <summary>The field named <paramref name="titleField"/>, or null.</summary>
    public static FieldView? TitleFieldOf(IReadOnlyList<FieldView> fields, string? titleField)
    {
        ArgumentNullException.ThrowIfNull(fields);

        return fields.FirstOrDefault(candidate => candidate.Name == titleField);
    }

    /// <summary>The record's display field as stored, or <paramref name="fallback"/> (its id) when it has none.</summary>
    public static string DisplayOf(object? record, FieldView? titleField, Guid fallback) =>
        (record is null || titleField is null ? null : titleField.Get(record))?.ToString() ?? fallback.ToString();

    /// <summary>
    /// Sends a command built from the record's id, at <paramref name="version"/> only when the command is version-guarded;
    /// a task is started as a run over the record instead, answering its
    /// <see cref="TaskRunStartedResponse"/> with no version sent or read.
    /// </summary>
    public static Task<ViewSendResult> SendFromIdAsync(ISerginDispatcher dispatcher, CommandView command, Guid id, RowVersion? version, string? label = null)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command.Task is not null
            ? StartManyAsync(dispatcher, command, [new TaskTarget(id, label)])
            : SendAsync(dispatcher, command, command.BuildFromId!(id), version);
    }

    /// <summary>
    /// Starts a task over one record as one run over <paramref name="targets"/>. The template is built from the first
    /// target (from its id); the run takes each item's ids from its target.
    /// </summary>
    internal static async Task<ViewSendResult> StartManyAsync(
        ISerginDispatcher dispatcher, CommandView command, IReadOnlyCollection<TaskTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(targets);

        TaskTarget first = targets.FirstOrDefault()
            ?? throw new ArgumentException("A task over one record is started over at least one record.", nameof(targets));

        return ResultOf(await dispatcher.StartTaskAsync(EntityTemplateOf(command, first), targets));
    }

    /// <summary>
    /// Starts a task over the collection (<see cref="CommandRole.CollectionTask"/>) as one run with no record, in the caller's
    /// current scope; a second start while that run is going is refused (<c>Tasks.AlreadyRunning</c>).
    /// </summary>
    internal static async Task<ViewSendResult> StartCollectionAsync(ISerginDispatcher dispatcher, CommandView command)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(command);

        return ResultOf(await dispatcher.StartTaskAsync(CollectionTemplateOf(command), []));
    }

    /// <summary>Builds a collection task's template, as its schedule saves it; refuses any other command.</summary>
    internal static ITaskCommand CollectionTemplateOf(CommandView command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command is { Role: CommandRole.CollectionTask, BuildWithoutTarget: { } build } && build() is ITaskCommand task
            ? task
            : throw new InvalidOperationException(
                $"{command.Request.FullName} cannot be started from a list view: it is not a task over the collection built with no arguments.");
    }

    private static ViewSendResult ResultOf(ErrorOr<TaskRunStartedResponse> started) =>
        started.IsError
            ? new ViewSendResult(null, null, started.Errors)
            : new ViewSendResult(started.Value, null, []);

    // A task over one record is built from the record's id (child tasks are refused at host start).
    private static ITaskCommand EntityTemplateOf(CommandView command, TaskTarget target)
    {
        if (command.BuildFromId is { } fromId && target.TargetId is { } id && fromId(id) is ITaskCommand task)
        {
            return task;
        }

        throw new InvalidOperationException(
            $"{command.Request.FullName} cannot be started from a view: it is not a task built from the record's id alone.");
    }

    /// <summary>Sends <paramref name="request"/>, built for <paramref name="command"/>, at <paramref name="version"/> only when the command is version-guarded.</summary>
    public static Task<ViewSendResult> SendAsync(ISerginDispatcher dispatcher, CommandView command, object request, RowVersion? version)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command.RequiresVersion
            ? command.Invoker.SendVersionedAsync(dispatcher, request, version)
            : command.Invoker.SendAsync(dispatcher, request);
    }

    /// <summary>
    /// Shows every error of a failed send, and on a stale answer runs <paramref name="reloadAsync"/> so the next write
    /// carries the version now there. Returns whether the send failed.
    /// </summary>
    public static async Task<bool> FailedAsync(ViewSendResult result, IUiErrorPresenter presenter, Func<Task> reloadAsync)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(presenter);
        ArgumentNullException.ThrowIfNull(reloadAsync);

        if (!result.IsError)
        {
            return false;
        }

        presenter.Notify(result.Errors);

        if (result.Errors.Any(VersionErrors.IsStale))
        {
            await reloadAsync();
        }

        return true;
    }

    /// <summary>
    /// A text's placeholder arguments: the record's properties (read through by the model's compiled reader), plus
    /// <c>{Name}</c>, its display field, unless the record has a <c>Name</c> of its own.
    /// </summary>
    public static Dictionary<string, object?> ArgumentsOf(
        Func<object, IReadOnlyDictionary<string, object?>>? reader, object? record, string display)
    {
        Dictionary<string, object?> arguments = record is null || reader is null
            ? new(StringComparer.Ordinal)
            : new(reader(record), StringComparer.Ordinal);

        arguments.TryAdd("Name", display);

        return arguments;
    }
}
