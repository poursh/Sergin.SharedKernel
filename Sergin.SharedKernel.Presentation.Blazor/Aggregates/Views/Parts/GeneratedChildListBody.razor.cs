using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Pages;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

/// <summary>What a generated child section asks of its body, whose item type it does not name.</summary>
internal interface IGeneratedChildListBody
{
    Task ReloadAsync();
}

/// <summary>
/// One row icon of a child list: the command, where it links (an edit page), and whether and why it is disabled. Decided
/// from the model and the row alone, so it can be read without rendering.
/// </summary>
/// <param name="Command">The row command.</param>
/// <param name="Href">The page an edit command links to, or null for a command that runs.</param>
/// <param name="Disabled">Disabled while the root's version is unknown, or when the command's <c>DisabledWhen</c> holds for the row.</param>
/// <param name="DisabledReason">The tooltip while the command's own rule disables it; null otherwise.</param>
internal sealed record ChildRowAction(CommandView Command, string? Href, bool Disabled, TextDefinition? DisabledReason);

/// <summary>
/// The table of a generated child section over <typeparamref name="TItem"/>, the child list query's item type: a column
/// per visible list field (the display field linking to the child's detail, a field with a <c>Format</c> text rendered
/// through it), quick search and sort labels from the list field set, and per row the edit icon (a link to the edit
/// page) and the Remove icon. Remove is a bulk action when the command is selectable.
/// </summary>
/// <remarks>
/// Removing a child changes the root, so it is sent at the root's version, which the root's view holds and passes in as
/// <see cref="Version"/>. Any answer but a refusal changes that version, so the body tells the root's view through
/// <see cref="OnChanged"/> to reload it, and reloads its own rows. A row icon is disabled while the version is unknown or
/// when its command's rule holds for the row (a child in use), with the rule's reason as tooltip.
/// </remarks>
public sealed partial class GeneratedChildListBody<TItem> : IGeneratedChildListBody
{
    // Compiled once per item type, which is the point of the field being per closed type.
#pragma warning disable S2743
    private static readonly Func<object, object?>? idReader = GeneratedViewSupport.IdReaderOf(typeof(TItem));
#pragma warning restore S2743

    private SerginEntityList<TItem> list = default!;
    private IReadOnlyList<ListColumn> columns = [];
    private FieldView? titleField;

    [Parameter]
    [EditorRequired]
    public ChildViewModel Child { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public Guid RootId { get; set; }

    /// <summary>The root's version, as its view last read it; null until it has one.</summary>
    [Parameter]
    public RowVersion? Version { get; set; }

    /// <summary>Raised after a command changed the root (or found its version stale), so its view reads it again.</summary>
    [Parameter]
    public EventCallback OnChanged { get; set; }

    /// <summary>Raised after a load whose total differs from the last one.</summary>
    [Parameter]
    public EventCallback<int> TotalChanged { get; set; }

    [Inject]
    private ISerginDispatcher Dispatcher { get; set; } = default!;

    [Inject]
    private IUiErrorPresenter ErrorPresenter { get; set; } = default!;

    [Inject]
    private IDialogService DialogService { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    [Inject]
    private ICultureFormatter Formatter { get; set; } = default!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = default!;

    private bool Searchable => Child.ListFieldSet is not null;

    private IReadOnlyList<SerginBulkAction<TItem>> BulkActions { get; set; } = [];

    /// <summary>The icons of <paramref name="item"/>'s row: the edit link, then each Remove, as the model and the root's version decide.</summary>
    internal static IReadOnlyList<ChildRowAction> RowActionsFor(ChildViewModel child, Guid rootId, RowVersion? version, TItem item)
    {
        ArgumentNullException.ThrowIfNull(child);

        Guid id = IdOf(item);
        List<ChildRowAction> actions = [];

        if (child.EditCommand is { Hidden: false, EditMode: SerginEditMode.Page } edit && child.Routes.Edit is { } editRoute)
        {
            actions.Add(RowAction(edit, editRoute(rootId, id), version, item));
        }

        foreach (CommandView remove in child.Commands.Where(command => command is { Role: CommandRole.RemoveChild, Hidden: false, BuildFromIds: not null }))
        {
            actions.Add(RowAction(remove, null, version, item));
        }

        return actions;
    }

    public Task ReloadAsync() => list.ReloadAsync();

    protected override void OnInitialized()
    {
        if (Child.List is null)
        {
            throw new InvalidOperationException($"{Child.Child.FullName} has no list query, so its section has nothing to load. Add an IChildListOf<…, {Child.Child.Name}>.");
        }

        titleField = GeneratedViewSupport.TitleFieldOf(Child.ListFields, Child.ListTitleField);
        columns = [.. Child.ListFields.Where(field => !field.Hidden).Select(field => new ListColumn(field, SortIdOf(field), field == titleField))];
        BulkActions = [.. BulkActionsOf()];
    }

    private static ChildRowAction RowAction(CommandView command, string? href, RowVersion? version, TItem item)
    {
        bool refused = command.DisabledWhen?.Invoke(item!) == true;

        return new ChildRowAction(command, href, version is null || refused, refused ? command.DisabledReason : null);
    }

    private static Guid IdOf(TItem item) =>
        idReader?.Invoke(item!) is Guid id
            ? id
            : throw new InvalidOperationException($"{typeof(TItem).FullName} has no Guid Id, so a generated child list cannot link or act on its rows.");

    // A column sorts by the list field named after its property, when the list field set declares it sortable.
    private string? SortIdOf(FieldView field) =>
        Child.ListFieldSet?.Fields
            .FirstOrDefault(candidate => candidate.Sortable && string.Equals(candidate.Id, field.Name, StringComparison.OrdinalIgnoreCase))
            ?.Id;

    private string? TextOf(ListColumn column, TItem item) => FieldValueText.Render(column.Field, column.Field.Get(item!), Formatter, Localizer);

    private Task OnTotalChanged(int count) => TotalChanged.InvokeAsync(count);

    private async Task<ErrorOr<ListQueryResponse<TItem>>> LoadAsync(SerginListRequest request, CancellationToken cancellationToken)
    {
        // The paging is already the query's: SerginEntityList turned MudTable's 0-based TableState.Page into the 1-based
        // PageIndex. The list has no filter panel, so no filter is sent.
        ListQueryInput input = new(request.Paggination, request.Term?.Value, [], [.. request.Sorting?.Sorts ?? []]);

        ViewSendResult result = await Child.List!.Invoker.SendAsync(Dispatcher, Child.List.Build(RootId, input), cancellationToken);

        if (result.IsError)
        {
            List<Error> errors = [.. result.Errors];
            return errors;
        }

        return (ListQueryResponse<TItem>)result.Value!;
    }

    // ---- Rows ----

    private SerginPageAction PageActionOf(ChildRowAction row, TItem item)
    {
        CommandView command = row.Command;

        return new SerginPageAction(
            command.Label,
            command.Icon,
            command.Kind,
            Href: row.Href,
            OnClick: row.Href is null ? EventCallback.Factory.Create(this, () => RemoveRowAsync(command, item)) : default,
            Permission: command.Permissions.FirstOrDefault(),
            AnyScope: command.AnyScope,
            Disabled: row.Disabled);
    }

    private async Task ChangedAsync()
    {
        await OnChanged.InvokeAsync();
        await list.ReloadAsync();
    }

    private async Task RemoveRowAsync(CommandView command, TItem item)
    {
        Guid id = IdOf(item);
        Dictionary<string, object?> arguments = GeneratedViewSupport.ArgumentsOf(Child.ListItemArguments, item, GeneratedViewSupport.DisplayOf(item, titleField, id));

        if (!await DialogService.ConfirmDeleteAsync(
                Localizer, command.ConfirmTitle ?? CommonTexts.Delete, command.ConfirmText ?? ShellTexts.CommandActions.DeleteConfirm, arguments, command.Label))
        {
            return;
        }

        ViewSendResult result = await GeneratedChildBulk.SendAsync(Dispatcher, command, RootId, id, Version);

        // An in-use child arrives as one validation error; a stale root as a version error, which reloads it.
        if (await GeneratedViewSupport.FailedAsync(result, ErrorPresenter, ChangedAsync))
        {
            return;
        }

        await ChangedAsync();
    }

    // ---- Bulk ----

    private IEnumerable<SerginBulkAction<TItem>> BulkActionsOf()
    {
        foreach (CommandView command in Child.Commands.Where(command => command is { Role: CommandRole.RemoveChild, Selectable: true, Hidden: false, BuildFromIds: not null }))
        {
            yield return new SerginBulkAction<TItem>(
                command.Label,
                command.Icon,
                SerginActionKind.Danger,
                items => RemoveSelectedAsync(command, items),
                command.Permissions.FirstOrDefault(),
                command.AnyScope);
        }
    }

    private async Task RemoveSelectedAsync(CommandView command, IReadOnlyList<TItem> items)
    {
        if (!await DialogService.ConfirmDeleteAsync(
                Localizer,
                command.ConfirmManyTitle ?? CommonTexts.Delete,
                command.ConfirmManyText ?? ShellTexts.CommandActions.DeleteManyConfirm,
                new { Count = Formatter.Number(items.Count) },
                command.Label))
        {
            return;
        }

        (SerginBulkOutcome outcome, _) = await GeneratedChildBulk.RemoveAsync(Dispatcher, command, RootId, Version, [.. items.Select(IdOf)]);

        outcome.Report(ErrorPresenter, Snackbar, Localizer, Formatter, command.BulkSucceededText ?? CommonTexts.BulkRemoved);

        // As for one remove: a success moved the version the root's view holds, and a stale answer found it out of date.
        if (outcome.Succeeded > 0 || outcome.Errors.Any(VersionErrors.IsStale))
        {
            await OnChanged.InvokeAsync();
        }

        await list.ReloadAsync();
    }

    /// <summary>One column: the list field, the list field id it sorts by (or null), and whether it links to the child.</summary>
    private sealed record ListColumn(FieldView Field, string? SortId, bool Links);
}
