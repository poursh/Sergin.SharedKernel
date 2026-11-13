using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.ScheduledTasks;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Domain.ScheduledTasks;
using Sergin.SharedKernel.Domain.Securities;
using Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Modules;
using Sergin.SharedKernel.Presentation.Blazor.Navigation;
using Sergin.SharedKernel.Presentation.Blazor.Pages;
using Sergin.SharedKernel.Presentation.Blazor.ScheduledTasks;
using Sergin.SharedKernel.Presentation.Blazor.Validation;
using Sergin.SharedKernel.Presentation.Blazor.Views;
using Sergin.SharedKernel.Presentation.Errors;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views;

/// <summary>
/// The detail view of <typeparamref name="TRoot"/>, generated from its <see cref="AggregateViewModel"/>: the record
/// loaded through its GetOne query (at its version when the root is <c>Versioned()</c>), the header's reload, copy-link and (with an audit query) information
/// buttons, a field per visible response property, an action per offered command, and, when a command is schedulable,
/// the record's scheduled tasks. A configuration's <see cref="SerginViewSlot.DetailFields"/> component replaces the
/// fields, and its <see cref="SerginViewSlot.DetailEntities"/> component renders above the child sections (<see cref="SerginGeneratedChildSection"/>,
/// one per child entity with a list query, given the record's version) and the scheduled tasks; the
/// <see cref="SerginViewSlot.DetailOverview"/> and <see cref="SerginViewSlot.DetailOverviewSummary"/> components fill the
/// overview (the entity overview query read once per load, in <c>Context.Overview</c>); all get
/// <c>Context</c>, an <see cref="AggregateViewContext"/>.
/// </summary>
/// <remarks>
/// A transition is sent from the record's id (at the held version when the command is version-guarded), its button
/// disabled while it runs; on success the configured text shows and the record is read again. A delete is confirmed,
/// sent at the held version and leaves for the list. A stale answer reloads the record. A schedulable command's button
/// is a split button holding Schedule… and View schedules (n) for a user who may manage scheduled tasks.
/// A root's set-property command is edited in its edit mode: Dialog, an action opening <see cref="SerginSetPropertyDialog"/>
/// prefilled from the record; Inline, for a user who holds its permissions, the detail field itself as a select or switch
/// (sent on change) or, for any other value, a <see cref="GeneratedInlineEditCard"/> below the fields. Each send goes
/// through <see cref="GeneratedSetProperty"/>; every error is shown and the record is read again after any outcome.
/// </remarks>
public sealed partial class SerginGeneratedDetailView<TRoot>
    where TRoot : class, IAggregateRoot
{
    private object? record;
    private RowVersion? version;
    private SerginProblem? problem;
    private bool canManageSchedules;
    private string? commandName;
    private TextDefinition? commandLabel;
    private SerginScheduledTasksSection? section;
    private GeneratedCommandActions commands = default!;
    private FieldView? titleField;
    private object? overview;
    private readonly Dictionary<Type, SerginGeneratedChildSection> childSections = [];
    private readonly Dictionary<string, CommandView> inlineChoices = new(StringComparer.Ordinal);
    private GeneratedSetProperty setProperty = default!;
    private IReadOnlyList<CommandView> inlineCards = [];
    private IReadOnlyDictionary<string, object?> recordValues = new Dictionary<string, object?>(StringComparer.Ordinal);

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

    [Inject]
    private ISerginFormValidator FormValidator { get; set; } = default!;

    [CascadingParameter]
    private SerginViewHost Host { get; set; } = SerginViewHost.Unhosted;

    private AggregateViewModel Model { get; set; } = default!;

    private Type? FieldsSlot => Model.Slots.GetValueOrDefault(SerginViewSlot.DetailFields);

    private Type? EntitiesSlot => Model.Slots.GetValueOrDefault(SerginViewSlot.DetailEntities);

    private IEnumerable<FieldView> VisibleFields => Model.DetailFields.Where(candidate => !candidate.Hidden);

    // A child entity with a list query is a section of the detail, after the entities slot and before the scheduled tasks.
    private IEnumerable<ChildViewModel> Sections => Model.Children.Where(child => child.List is not null);

    // Each section is kept by its child type, so a reload asks each to load its rows again without remounting it.
    private RenderFragment SectionsFragment => builder =>
    {
        foreach (ChildViewModel child in Sections)
        {
            builder.OpenComponent<SerginGeneratedChildSection>(0);
            builder.SetKey($"{Id}:{child.Child.FullName}");
            builder.AddAttribute(1, nameof(SerginGeneratedChildSection.Model), Model);
            builder.AddAttribute(2, nameof(SerginGeneratedChildSection.Child), child);
            builder.AddAttribute(3, nameof(SerginGeneratedChildSection.RootId), Id);
            builder.AddAttribute(4, nameof(SerginGeneratedChildSection.Version), (object?)version);
            builder.AddAttribute(5, nameof(SerginGeneratedChildSection.OnChanged), ChildChanged);
            builder.AddComponentReferenceCapture(6, section => childSections[child.Child] = (SerginGeneratedChildSection)section);
            builder.CloseComponent();
        }
    };

    // A child command moved the root's version (or found it stale): the record is read again, and the section reloads its own rows.
    private EventCallback ChildChanged => EventCallback.Factory.Create(this, LoadAsync);

    private AggregateViewContext Context => new(record, version, ReloadAsync, [Id], overview);

    private RenderFragment? OverviewFragment => record is null ? null : SlotOf(SerginViewSlot.DetailOverview);

    private RenderFragment? OverviewSummaryFragment => record is null ? null : SlotOf(SerginViewSlot.DetailOverviewSummary);

    private Dictionary<string, object?> SlotParameters => new(StringComparer.Ordinal) { ["Context"] = Context };

    // The record's display field as stored, or its id when it has none.
    private string DisplayText => GeneratedViewSupport.DisplayOf(record, titleField, Id);

    // The list section step — the nav entry naming the list route, else the list's title — then the record, or the
    // root's name until it loads and beside the problem panel.
    private IReadOnlyList<SerginBreadcrumb> Trail =>
    [
        .. ListStep(),
        record is null ? new(Model.Singular) : SerginBreadcrumb.Data(DisplayText),
    ];

    private IReadOnlyList<SerginPageAction> Actions => record is null ? [] : commands.For(Model, Id, MenuItemsOf, record: record, links: ViewLinkPlacement.AfterCommands);

    protected override async Task OnInitializedAsync()
    {
        Model = Views.For<TRoot>();

        if (Model.GetOne is null)
        {
            throw new InvalidOperationException(
                $"{typeof(TRoot).FullName} has no GetOne query, so its detail view has nothing to load. Add an IGetOneQuery<{typeof(TRoot).Name}, …>.");
        }

        commands = new GeneratedCommandActions(this, RunAsync, DeleteAsync, EditAsync);
        setProperty = new GeneratedSetProperty(FormValidator);
        titleField = GeneratedViewSupport.TitleFieldOf(Model.DetailFields, Model.DetailTitleField);
        await LoadInlineCommandsAsync();

        canManageSchedules = Model.HasSchedulableCommand && await ScopeAuthorization.CurrentUserHasAsync(ScheduledTaskPermissions.Manage);
    }

    protected override Task OnParametersSetAsync() => LoadAsync();

    private RenderFragment? SlotOf(SerginViewSlot slot) =>
        Model.Slots.GetValueOrDefault(slot) is { } component
            ? builder =>
            {
                builder.OpenComponent(0, component);
                builder.AddAttribute(1, "Context", Context);
                builder.CloseComponent();
            }
            : null;

    private IEnumerable<SerginBreadcrumb> ListStep() =>
        GeneratedViewSupport.ListStep(Model, Catalog) is { } step ? [step] : [];

    // Keeps the version the record was read at (a Versioned() root's), so a guarded write can say which record it means.
    private async Task LoadAsync()
    {
        ViewSendResult loaded = await Model.LoadRecordAsync(Dispatcher, Id);

        if (loaded.IsError)
        {
            record = null;
            version = null;
            overview = null;
            problem = ErrorPresenter.Present(loaded.Errors[0]);

            return;
        }

        problem = null;
        record = loaded.Value;
        version = loaded.Version;
        recordValues = Model.DetailArguments!(loaded.Value!);
        overview = Model.EntityOverview is { } entityOverview
            && (Model.Slots.ContainsKey(SerginViewSlot.DetailOverview) || Model.Slots.ContainsKey(SerginViewSlot.DetailOverviewSummary))
            ? await GeneratedViewSupport.LoadOverviewAsync(Dispatcher, entityOverview.Invoker, entityOverview.Build(Id))
            : null;
    }

    // What a slot component or a configured menu item calls: it runs outside this view's own handlers, so it re-renders.
    private async Task ReloadAsync()
    {
        await LoadAsync();

        foreach (SerginGeneratedChildSection childSection in childSections.Values)
        {
            await childSection.ReloadAsync();
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task<ErrorOr<AuditInfo>> LoadAuditAsync()
    {
        ViewSendResult result = await Model.Audit!.Invoker.SendAsync(Dispatcher, Model.Audit.Build(Id));

        if (result.IsError)
        {
            List<Error> errors = [.. result.Errors];
            return errors;
        }

        return (AuditInfo)result.Value!;
    }

    private Task<ViewSendResult> SendAsync(CommandView command) =>
        GeneratedViewSupport.SendFromIdAsync(Dispatcher, command, Id, version);

    private async Task RunAsync(CommandView command)
    {
        ViewSendResult result = await commands.WhileRunningAsync(command, () => SendAsync(command));

        if (await GeneratedViewSupport.FailedAsync(result, ErrorPresenter, LoadAsync))
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

        if (await GeneratedViewSupport.FailedAsync(result, ErrorPresenter, LoadAsync))
        {
            return;
        }

        if (Model.Routes.List is { } list)
        {
            await Host.LeaveAsync(list);
        }
    }

    // The inline set-property commands this user may send (presentation only: the pipeline still checks), split into the
    // choices a detail field becomes and the cards below the fields.
    private async Task LoadInlineCommandsAsync()
    {
        List<CommandView> cards = [];

        foreach (CommandView command in Model.Commands.Where(command => command is { Hidden: false, EditMode: SerginEditMode.Inline, ValueField: not null }))
        {
            if (!await HoldsAsync(command))
            {
                continue;
            }

            if (command.ValueField!.Kind is FormInputKind.Select or FormInputKind.Switch)
            {
                inlineChoices[command.ValueField.Name] = command;
            }
            else
            {
                cards.Add(command);
            }
        }

        inlineCards = cards;
    }

    private async Task<bool> HoldsAsync(CommandView command)
    {
        foreach (PermissionDefinition permission in command.Permissions)
        {
            bool held = command.AnyScope
                ? await ScopeAuthorization.CurrentUserHoldsAnywhereAsync(permission)
                : await ScopeAuthorization.CurrentUserHasAsync(permission);

            if (!held)
            {
                return false;
            }
        }

        return true;
    }

    private CommandView? InlineChoiceOf(FieldView field) => inlineChoices.GetValueOrDefault(field.Name);

    private bool IsInlineBusy(FieldView field) => InlineChoiceOf(field) is { } command && commands.IsRunning(command);

    private EventCallback<object?> InlineChanged(FieldView field) =>
        InlineChoiceOf(field) is { } command
            ? EventCallback.Factory.Create<object?>(this, value => SetPropertyAsync(command, value))
            : default;

    private EventCallback<object?> InlineSubmitted(CommandView command) =>
        EventCallback.Factory.Create<object?>(this, value => SetPropertyAsync(command, value));

    // The record's value of the command's property, as its input edits it; null when the record has none.
    private object? ValueOf(CommandView command) => recordValues.GetValueOrDefault(command.ValueField!.Name);

    private async Task EditAsync(CommandView command)
    {
        DialogParameters<SerginSetPropertyDialog> parameters = new()
        {
            { dialog => dialog.Command, command },
            { dialog => dialog.Id, Id },
            { dialog => dialog.Value, ValueOf(command) },
            { dialog => dialog.Submit, value => SetPropertyAsync(command, value) },
        };

        await DialogService.ShowAsync<SerginSetPropertyDialog>(
            string.Empty, parameters, new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true });
    }

    // Sends one value; shows every error and reads the record again whatever the outcome. True when it was sent.
    private async Task<bool> SetPropertyAsync(CommandView command, object? value)
    {
        SetPropertyStatus status = await commands.WhileRunningAsync(
            command, () => setProperty.SubmitAsync(Dispatcher, command, Id, value, version));

        if (status != SetPropertyStatus.Sent)
        {
            ErrorPresenter.Notify(setProperty.Errors);
        }
        else if (command.SucceededText is { } succeeded)
        {
            Snackbar.Add(Localizer.Text(succeeded, PlaceholderArguments()), Severity.Success);
        }

        await ReloadAsync();

        return status == SetPropertyStatus.Sent;
    }

    // The record's properties (value objects read through, by the model's compiled reader) fill a text's placeholders,
    // plus {Name} — its display field — unless the record has a Name of its own.
    private Dictionary<string, object?> PlaceholderArguments() =>
        GeneratedViewSupport.ArgumentsOf(Model.DetailArguments, record, DisplayText);

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
                EventCallback.Factory.Create(this, () => ViewSchedulesAsync(name, command.Label)),
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

    // Narrows the section to the command's schedules (its chip naming the command by its label) and brings it into view.
    private async Task ViewSchedulesAsync(string name, TextDefinition label)
    {
        commandName = name;
        commandLabel = label;
        StateHasChanged();

        await ScrollManager.ScrollIntoViewAsync($"#{SerginScheduledTasksSection.ElementId}", ScrollBehavior.Smooth);
    }

    // The section counted its schedules: the View schedules entries show the new counts.
    private void OnSectionLoaded() => StateHasChanged();
}
