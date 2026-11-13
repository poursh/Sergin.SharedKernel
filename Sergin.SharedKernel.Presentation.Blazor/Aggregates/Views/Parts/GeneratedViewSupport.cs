using System.Reflection;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Modules;
using Sergin.SharedKernel.Domain;
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

    /// <summary>Sends a command built from the record's id, at <paramref name="version"/> only when the command is version-guarded.</summary>
    public static Task<ViewSendResult> SendFromIdAsync(ISerginDispatcher dispatcher, CommandView command, Guid id, RowVersion? version)
    {
        ArgumentNullException.ThrowIfNull(command);

        object request = command.BuildFromId!(id);

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
