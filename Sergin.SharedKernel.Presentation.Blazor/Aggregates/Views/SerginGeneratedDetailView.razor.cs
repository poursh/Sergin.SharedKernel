using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Application.ScheduledTasks;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Domain.ScheduledTasks;
using Sergin.SharedKernel.Domain.Securities;
using Sergin.SharedKernel.Modules;
using Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Modules;
using Sergin.SharedKernel.Presentation.Blazor.Navigation;
using Sergin.SharedKernel.Presentation.Blazor.Pages;
using Sergin.SharedKernel.Presentation.Blazor.ScheduledTasks;
using Sergin.SharedKernel.Presentation.Blazor.Views;
using Sergin.SharedKernel.Presentation.Errors;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views;

/// <summary>
/// The detail view of <typeparamref name="TRoot"/>, generated from its <see cref="AggregateViewModel"/>: the record
/// loaded through its GetOne query at its version, the header's reload, copy-link and (with an audit query) information
/// buttons, a field per visible response property, an action per offered command, and, when a command is schedulable,
/// the record's scheduled tasks. A configuration's <see cref="SerginViewSlot.DetailFields"/> component replaces the
/// fields, and its <see cref="SerginViewSlot.DetailEntities"/> component renders above the scheduled tasks; both get
/// <c>Context</c>, an <see cref="AggregateViewContext"/>.
/// </summary>
/// <remarks>
/// A transition is sent from the record's id (at the held version when the command is version-guarded), its button
/// disabled while it runs; on success the configured text shows and the record is read again. A delete is confirmed,
/// sent at the held version and leaves for the list. A stale answer reloads the record. A schedulable command's button
/// is a split button holding Schedule… and View schedules (n) for a user who may manage scheduled tasks.
/// </remarks>
public sealed partial class SerginGeneratedDetailView<TRoot>
    where TRoot : class, IAggregateRoot
{
    private object? record;
    private RowVersion? version;
    private SerginProblem? problem;
    private bool canManageSchedules;
    private string? commandName;
    private SerginScheduledTasksSection? section;
    private GeneratedCommandActions commands = default!;

    [Parameter]
    public Guid Id { get; set; }

    [Inject]
    private AggregateViewRegistry Views { get; set; } = default!;

    [Inject]
    private SerginUiModuleCatalog Catalog { get; set; } = default!;

    [Inject]
    private ISerginDispatcher Dispatcher { get; set; } = default!;

    [Inject]
    private IUiErrorPresenter ErrorPresenter { get; set; } = default!;

    [Inject]
    private IDialogService DialogService { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = default!;

    [Inject]
    private IScopeAuthorization ScopeAuthorization { get; set; } = default!;

    [Inject]
    private IScrollManager ScrollManager { get; set; } = default!;

    [CascadingParameter]
    private SerginViewHost Host { get; set; } = SerginViewHost.Unhosted;

    private AggregateViewModel Model { get; set; } = default!;

    private Type? FieldsSlot => Model.Slots.GetValueOrDefault(SerginViewSlot.DetailFields);

    private Type? EntitiesSlot => Model.Slots.GetValueOrDefault(SerginViewSlot.DetailEntities);

    private IEnumerable<FieldView> VisibleFields => Model.DetailFields.Where(candidate => !candidate.Hidden);

    private AggregateViewContext Context => new(record, version, ReloadAsync, [Id]);

    private Dictionary<string, object?> SlotParameters => new(StringComparer.Ordinal) { ["Context"] = Context };

    // The record's display field as stored, or its id when it has none.
    private string DisplayText
    {
        get
        {
            FieldView? title = Model.DetailFields.FirstOrDefault(candidate => candidate.Name == Model.DetailTitleField);
            object? value = record is null || title is null ? null : ValueObjects.Unwrap(title.Get(record));

            return value?.ToString() ?? Id.ToString();
        }
    }

    // The list section step — the nav entry naming the list route, else the list's title — then the record, or the
    // root's name until it loads and beside the problem panel.
    private IReadOnlyList<SerginBreadcrumb> Trail =>
    [
        .. ListStep(),
        record is null ? new(Model.Singular) : SerginBreadcrumb.Data(DisplayText),
    ];

    private IReadOnlyList<SerginPageAction> Actions => record is null ? [] : commands.For(Model, Id, MenuItemsOf);

    protected override async Task OnInitializedAsync()
    {
        Model = Views.For<TRoot>();

        if (Model.GetOneInvoker is null || Model.BuildGetOne is null)
        {
            throw new InvalidOperationException(
                $"{typeof(TRoot).FullName} has no GetOne query, so its detail view has nothing to load. Add an IGetOneQuery<{typeof(TRoot).Name}, …>.");
        }

        commands = new GeneratedCommandActions(this, RunAsync, DeleteAsync);

        canManageSchedules = Model.HasSchedulableCommand && await ScopeAuthorization.CurrentUserHasAsync(ScheduledTaskPermissions.Manage);
    }

    protected override Task OnParametersSetAsync() => LoadAsync();

    private IEnumerable<SerginBreadcrumb> ListStep()
    {
        if (Model.Routes.List is not { } list)
        {
            return [];
        }

        SerginNavItem? item = Catalog.NavItems.FirstOrDefault(candidate => string.Equals(candidate.Href, list, StringComparison.OrdinalIgnoreCase));

        return [item is null ? new SerginBreadcrumb(Model.Title, list) : SerginBreadcrumb.Of(item)];
    }

    // Keeps the version the record was read at, so a guarded write can say which record it means.
    private async Task LoadAsync()
    {
        ViewSendResult loaded = await Model.GetOneInvoker!.SendVersionedAsync(Dispatcher, Model.BuildGetOne!(Id), expected: null);

        if (loaded.IsError)
        {
            record = null;
            version = null;
            problem = ErrorPresenter.Present(loaded.Errors[0]);

            return;
        }

        problem = null;
        record = loaded.Value;
        version = loaded.Version;
    }

    // What a slot component or a configured menu item calls: it runs outside this view's own handlers, so it re-renders.
    private async Task ReloadAsync()
    {
        await LoadAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task<ErrorOr<AuditInfo>> LoadAuditAsync()
    {
        ViewSendResult result = await Model.AuditInvoker!.SendAsync(Dispatcher, Model.BuildAudit!(Id));

        if (result.IsError)
        {
            List<Error> errors = [.. result.Errors];
            return errors;
        }

        return (AuditInfo)result.Value!;
    }

    private Task<ViewSendResult> SendAsync(CommandView command)
    {
        object request = command.BuildFromId!(Id);

        return command.RequiresVersion
            ? command.Invoker.SendVersionedAsync(Dispatcher, request, version)
            : command.Invoker.SendAsync(Dispatcher, request);
    }

    private async Task RunAsync(CommandView command)
    {
        ViewSendResult result = await commands.WhileRunningAsync(command, () => SendAsync(command));

        if (await FailedAsync(result))
        {
            return;
        }

        if (command.SucceededText is { } succeeded)
        {
            Snackbar.Add(Localizer.Text(succeeded, PlaceholderArguments()), Severity.Success);
        }

        await LoadAsync();
    }

    private async Task DeleteAsync(CommandView command)
    {
        if (!await DialogService.ConfirmDeleteAsync(
                Localizer, command.ConfirmTitle ?? CommonTexts.Delete, command.ConfirmText ?? ShellTexts.CommandActions.DeleteConfirm, PlaceholderArguments()))
        {
            return;
        }

        ViewSendResult result = await commands.WhileRunningAsync(command, () => SendAsync(command));

        if (await FailedAsync(result))
        {
            return;
        }

        if (Model.Routes.List is { } list)
        {
            await Host.LeaveAsync(list);
        }
    }

    // Shows every error; on a stale answer, reads the record again so the next write carries the version it now has.
    private async Task<bool> FailedAsync(ViewSendResult result)
    {
        if (!result.IsError)
        {
            return false;
        }

        ErrorPresenter.Notify(result.Errors);

        if (result.Errors.Any(VersionErrors.IsStale))
        {
            await LoadAsync();
        }

        return true;
    }

    // The record's properties fill a text's placeholders, plus {Name}: its display field.
    private Dictionary<string, object?> PlaceholderArguments()
    {
        Dictionary<string, object?> arguments = record is null
            ? new(StringComparer.Ordinal)
            : new(TextPlaceholders.ArgumentsOf(record), StringComparer.Ordinal);

        arguments.TryAdd("Name", DisplayText);

        return arguments;
    }

    // Schedule… and View schedules for a schedulable command (to a user who may manage scheduled tasks), then the
    // configuration's own entries.
    private IReadOnlyList<SerginMenuItem> MenuItemsOf(CommandView command)
    {
        List<SerginMenuItem> items = [];
        PermissionDefinition? permission = command.Permissions.FirstOrDefault();

        if (command.ScheduleName is { } name && canManageSchedules)
        {
            items.Add(new SerginMenuItem(
                ScheduledTaskUiTexts.ScheduleAction,
                Icons.Material.Outlined.Schedule,
                EventCallback.Factory.Create(this, () => ScheduleAsync(command)),
                permission,
                command.AnyScope));
            items.Add(new SerginMenuItem(
                ScheduledTaskUiTexts.ViewSchedules,
                Icons.Material.Outlined.EventNote,
                EventCallback.Factory.Create(this, () => ViewSchedulesAsync(name)),
                permission,
                command.AnyScope,
                new { Count = section?.CountOf(name) ?? 0 }));
        }

        foreach (CommandMenuItemView configured in command.MenuItems)
        {
            items.Add(new SerginMenuItem(
                configured.Label,
                configured.Icon,
                EventCallback.Factory.Create(this, () => configured.OnClick(Context)),
                configured.Permission,
                command.AnyScope));
        }

        return items;
    }

    private async Task ScheduleAsync(CommandView command)
    {
        IReadOnlyCollection<ScheduledTaskTarget> targets = [new ScheduledTaskTarget(Id, null)];

        // Saved, run once or cancelled: the section reads its lists again either way (a one-off run starts Running).
        await DialogService.ShowScheduleAsync(new SerginScheduleRequest(
            Model.Schema,
            command.Label,
            () => (IBaseCommand)command.BuildFromId!(Id),
            targets,
            [DisplayText]));

        if (section is not null)
        {
            await section.ReloadAsync();
        }
    }

    // Narrows the section to the command's schedules and brings it into view.
    private async Task ViewSchedulesAsync(string name)
    {
        commandName = name;
        StateHasChanged();

        await ScrollManager.ScrollIntoViewAsync($"#{SerginScheduledTasksSection.ElementId}", ScrollBehavior.Smooth);
    }

    // The section counted its schedules: the View schedules entries show the new counts.
    private void OnSectionLoaded() => StateHasChanged();
}
