using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.ScheduledTasks;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Domain.ScheduledTasks;
using Sergin.SharedKernel.Domain.Securities;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Pages;
using Sergin.SharedKernel.Presentation.Blazor.ScheduledTasks;
using Sergin.SharedKernel.Presentation.Blazor.Times;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

/// <summary>The command roles a generated list row offers: as icons, and in its menu.</summary>
internal static class GeneratedLists
{
    public static IReadOnlySet<CommandRole> Icons { get; } = new HashSet<CommandRole> { CommandRole.Update, CommandRole.Delete };

    public static IReadOnlySet<CommandRole> Menu { get; } = new HashSet<CommandRole> { CommandRole.Transition };
}

/// <summary>What a generated list view asks of its body, whose item type it does not name.</summary>
internal interface IGeneratedListBody
{
    Task ReloadAsync();
}

/// <summary>
/// The table of a generated list over <typeparamref name="TItem"/>, the list query's item type: a column per visible
/// list field, its header a sort label when the list field set declares the field sortable, the display field linking
/// to the record; quick search (when the list declares a field set), the filter panel, and per row the Edit and Delete
/// icons and a menu of the root's transitions. Every selectable command is a bulk action.
/// </summary>
/// <remarks>
/// A row's delete is confirmed and sent at the version read just before it, as the detail page sends it; a
/// version-guarded transition likewise. A bulk run sends each row's command in turn and reports one count and each
/// distinct error once. A schedulable command's row entry and bulk button carry Schedule… for a user who may manage
/// scheduled tasks; the list offers no View schedules. The item's id and arguments are read by readers compiled once
/// per item type.
/// </remarks>
public sealed partial class GeneratedListBody<TItem> : IGeneratedListBody
{
    // Compiled once per item type, which is the point of the field being per closed type.
#pragma warning disable S2743
    private static readonly Func<object, object?>? idReader = GeneratedViewSupport.IdReaderOf(typeof(TItem));
#pragma warning restore S2743

    private SerginAggregateList<TItem> list = default!;
    private GeneratedCommandActions commands = default!;
    private GeneratedFilterState filters = default!;
    private IReadOnlyList<ListColumn> columns = [];
    private FieldView? titleField;
    private bool canManageSchedules;

    [Parameter]
    [EditorRequired]
    public AggregateViewModel Model { get; set; } = default!;

    /// <summary>The parent's id of a list under a parent: every list query is sent with it.</summary>
    [Parameter]
    public Guid? ParentId { get; set; }

    /// <summary>Raised after a command changed records, so the view can refresh what else it shows (the overview).</summary>
    [Parameter]
    public EventCallback OnChanged { get; set; }

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

    [Inject]
    private IScopeAuthorization ScopeAuthorization { get; set; } = default!;

    [Inject]
    private IUiTimeZoneStore TimeZones { get; set; } = default!;

    private bool Searchable => Model.ListFieldSet is not null;

    private IReadOnlyList<SerginBulkAction<TItem>> BulkActions { get; set; } = [];

    private RenderFragment? FilterPanel => filters.IsEmpty
        ? null
        : builder =>
        {
            builder.OpenComponent<GeneratedFilterPanel>(0);
            builder.AddAttribute(1, nameof(GeneratedFilterPanel.State), filters);
            builder.CloseComponent();
        };

    private Func<IReadOnlyList<SerginAppliedFilter>>? BuildFilters => filters.IsEmpty ? null : filters.Build;

    /// <summary>
    /// The Schedule dialog's request for running <paramref name="command"/> on <paramref name="items"/>: one target per
    /// row, shown by its display field; the template's own id is ignored.
    /// </summary>
    internal static SerginScheduleRequest ScheduleRequestFor(AggregateViewModel model, CommandView command, IReadOnlyList<TItem> items)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(items);

        Func<Guid, object> build = command.BuildFromId
            ?? throw new InvalidOperationException($"{command.Request.FullName} cannot be scheduled from a list: its constructor is not id-only.");
        FieldView? title = GeneratedViewSupport.TitleFieldOf(model.ListFields, model.ListTitleField);

        return new SerginScheduleRequest(
            model.Schema,
            command.Label,
            () => (IBaseCommand)build(Guid.Empty),
            [.. items.Select(item => new ScheduledTaskTarget(IdOf(item), null))],
            [.. items.Select(item => GeneratedViewSupport.DisplayOf(item, title, IdOf(item)))]);
    }

    public Task ReloadAsync() => list.ReloadAsync();

    protected override async Task OnInitializedAsync()
    {
        if (Model.List is null)
        {
            throw new InvalidOperationException($"{Model.Root.FullName} has no list query, so its list view has nothing to load. Add an IListOf<{Model.Root.Name}>.");
        }

        commands = new GeneratedCommandActions(this, _ => Task.CompletedTask, _ => Task.CompletedTask);
        filters = new GeneratedFilterState(Model, new GeneratedFilterServices(Dispatcher, ErrorPresenter, Localizer, Formatter, TimeZones));
        titleField = GeneratedViewSupport.TitleFieldOf(Model.ListFields, Model.ListTitleField);
        columns = GeneratedViewSupport.ColumnsOf(Model.ListFields, Model.ListFieldSet, titleField);

        canManageSchedules = Model.HasSchedulableCommand && await ScopeAuthorization.CurrentUserHasAsync(ScheduledTaskPermissions.Manage);
        BulkActions = [.. BulkActionsOf()];
    }

    private static Guid IdOf(TItem item) => GeneratedViewSupport.IdOf(idReader, item!);

    private string? TextOf(ListColumn column, TItem item) => FieldValueText.Render(column.Field, column.Field.Get(item!), Formatter, Localizer);

    private async Task<ErrorOr<ListQueryResponse<TItem>>> LoadAsync(SerginListRequest request, CancellationToken cancellationToken)
    {
        // The paging is already the query's: SerginAggregateList turned MudTable's 0-based TableState.Page into the
        // 1-based PageIndex (state.Page + 1).
        ListQueryInput input = new(
            request.Paggination,
            request.Term?.Value,
            [.. request.Filtering?.Filters ?? []],
            [.. request.Sorting?.Sorts ?? []],
            ParentId);

        ViewSendResult result = await Model.List!.Invoker.SendAsync(Dispatcher, Model.List.Build(input), cancellationToken);

        if (result.IsError)
        {
            List<Error> errors = [.. result.Errors];
            return errors;
        }

        return (ListQueryResponse<TItem>)result.Value!;
    }

    private void OnFilterRemoved(SerginAppliedFilter filter) => filters.Clear(filter.Id);

    private void OnFiltersReset() => filters.ClearAll();

    private Task OnFilterPanelOpenedAsync() => filters.LoadAsync();

    private async Task ChangedAsync()
    {
        await list.ReloadAsync();
        await OnChanged.InvokeAsync();
    }

    // ---- Rows ----

    private IReadOnlyList<SerginPageAction> IconActionsOf(TItem item) =>
        commands.For(
            Model, IdOf(item), _ => [], GeneratedLists.Icons, delete: command => DeleteRowAsync(command, item), record: item, links: ViewLinkPlacement.BeforeCommands);

    private IReadOnlyList<SerginPageAction> MenuActionsOf(TItem item) =>
        commands.For(
            Model, IdOf(item), command => RowMenuItemsOf(command, item), GeneratedLists.Menu, run: command => RunRowAsync(command, item), record: item);

    private AggregateViewContext ContextOf(IReadOnlyList<TItem> items) =>
        new(items.Count == 1 ? items[0] : null, Version: null, ChangedAsync, [.. items.Select(IdOf)]);

    // The item's properties (value objects read through) fill a text's placeholders, plus {Name}, its display field.
    private Dictionary<string, object?> ArgumentsOf(TItem item) =>
        GeneratedViewSupport.ArgumentsOf(Model.ListItemArguments, item, GeneratedViewSupport.DisplayOf(item, titleField, IdOf(item)));

    private List<SerginMenuItem> RowMenuItemsOf(CommandView command, TItem item)
    {
        List<SerginMenuItem> items = [];

        if (command.ScheduleName is not null && command.BuildFromId is not null && canManageSchedules)
        {
            items.Add(new SerginMenuItem(
                ScheduledTaskUiTexts.ScheduleAction,
                Icons.Material.Outlined.Schedule,
                EventCallback.Factory.Create(this, () => DialogService.ShowScheduleAsync(ScheduleRequestFor(Model, command, [item]))),
                command.Permissions.FirstOrDefault(),
                command.AnyScope));
        }

        foreach (CommandMenuItemView configured in command.MenuItems)
        {
            items.Add(new SerginMenuItem(
                configured.Label,
                configured.Icon,
                EventCallback.Factory.Create(this, () => configured.OnClick(ContextOf([item]))),
                configured.Permission,
                command.AnyScope));
        }

        return items;
    }

    private async Task RunRowAsync(CommandView command, TItem item)
    {
        ErrorOr<Success> result = await commands.WhileRunningAsync(command, () => SendAsync(command, item));

        if (result.IsError)
        {
            ErrorPresenter.Notify(result.Errors);
        }
        else if (command.SucceededText is { } succeeded)
        {
            Snackbar.Add(Localizer.Text(succeeded, ArgumentsOf(item)), Severity.Success);
        }

        await ChangedAsync();
    }

    private async Task DeleteRowAsync(CommandView command, TItem item)
    {
        if (!await DialogService.ConfirmDeleteAsync(
                Localizer, command.ConfirmTitle ?? CommonTexts.Delete, command.ConfirmText ?? ShellTexts.CommandActions.DeleteConfirm, ArgumentsOf(item)))
        {
            return;
        }

        ErrorOr<Success> result = await commands.WhileRunningAsync(command, () => SendAsync(command, item));

        if (result.IsError)
        {
            ErrorPresenter.Notify(result.Errors);
        }

        await ChangedAsync();
    }

    // A version-guarded command is checked against the version read just before it, as the detail page holds it; a stale
    // answer surfaces as an error and the reload shows the list as it now is.
    private async Task<ErrorOr<Success>> SendAsync(CommandView command, TItem item)
    {
        Guid id = IdOf(item);
        object request = command.BuildFromId!(id);

        if (!command.RequiresVersion)
        {
            ViewSendResult sent = await command.Invoker.SendAsync(Dispatcher, request);
            return sent.IsError ? ErrorsOf(sent) : Result.Success;
        }

        RowVersion? version = null;

        if (Model.GetOne is { } getOne)
        {
            ViewSendResult loaded = await getOne.Invoker.SendVersionedAsync(Dispatcher, getOne.Build(id), expected: null);

            if (loaded.IsError)
            {
                return ErrorsOf(loaded);
            }

            version = loaded.Version;
        }

        ViewSendResult result = await command.Invoker.SendVersionedAsync(Dispatcher, request, version);

        return result.IsError ? ErrorsOf(result) : Result.Success;
    }

    private static List<Error> ErrorsOf(ViewSendResult result) => [.. result.Errors];

    // ---- Bulk ----

    private IEnumerable<SerginBulkAction<TItem>> BulkActionsOf()
    {
        foreach (CommandView command in Model.Commands.Where(command => command is { Selectable: true, Hidden: false, BuildFromId: not null, AppliesWhen: null }))
        {
            PermissionDefinition? permission = command.Permissions.FirstOrDefault();

            if (command.Role == CommandRole.Delete)
            {
                yield return new SerginBulkAction<TItem>(
                    command.Label, command.Icon, SerginActionKind.Danger, items => DeleteSelectedAsync(command, items), permission, command.AnyScope);
            }
            else if (command.Role == CommandRole.Transition)
            {
                IReadOnlyList<SerginBulkMenuItem<TItem>> menu = command.ScheduleName is not null && canManageSchedules
                    ?
                    [
                        new SerginBulkMenuItem<TItem>(
                            ScheduledTaskUiTexts.ScheduleAction,
                            Icons.Material.Outlined.Schedule,
                            items => DialogService.ShowScheduleAsync(ScheduleRequestFor(Model, command, items)),
                            permission,
                            command.AnyScope),
                    ]
                    : [];

                yield return new SerginBulkAction<TItem>(
                    command.Label,
                    command.Icon,
                    command.Kind,
                    items => RunSelectedAsync(command, items),
                    permission,
                    command.AnyScope,
                    menu.Count > 0 ? menu : null);
            }
        }
    }

    private async Task RunSelectedAsync(CommandView command, IReadOnlyList<TItem> items)
    {
        SerginBulkOutcome outcome = await SerginBulk.RunAsync(items, item => SendAsync(command, item));

        outcome.Report(ErrorPresenter, Snackbar, Localizer, Formatter, command.BulkSucceededText ?? ShellTexts.CommandActions.BulkDone);
        await ChangedAsync();
    }

    private async Task DeleteSelectedAsync(CommandView command, IReadOnlyList<TItem> items)
    {
        if (!await DialogService.ConfirmDeleteAsync(
                Localizer,
                command.ConfirmManyTitle ?? CommonTexts.Delete,
                command.ConfirmManyText ?? ShellTexts.CommandActions.DeleteManyConfirm,
                new { Count = Formatter.Number(items.Count) }))
        {
            return;
        }

        SerginBulkOutcome outcome = await SerginBulk.RunAsync(items, item => SendAsync(command, item));

        outcome.Report(ErrorPresenter, Snackbar, Localizer, Formatter, command.BulkSucceededText ?? CommonTexts.BulkDeleted);
        await ChangedAsync();
    }
}
