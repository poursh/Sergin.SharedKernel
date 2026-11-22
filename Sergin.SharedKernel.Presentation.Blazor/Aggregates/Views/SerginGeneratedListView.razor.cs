using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Application.Tasks;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Domain.Securities;
using Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Actions;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Modules;
using Sergin.SharedKernel.Presentation.Blazor.Navigation;
using Sergin.SharedKernel.Presentation.Blazor.Scopes;
using Sergin.SharedKernel.Presentation.Blazor.Tasks;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views;

/// <summary>
/// The list view of <typeparamref name="TRoot"/>, generated from its <see cref="AggregateViewModel"/>: the list's title,
/// its nav entry as the trail, a reload button, the configuration's <see cref="SerginViewSlot.ListOverview"/> and
/// <see cref="SerginViewSlot.ListOverviewSummary"/> components (given <c>Context</c>, an <see cref="AggregateViewContext"/>
/// with no record), the create command as the primary action, and the table (<see cref="GeneratedListBody{TItem}"/>,
/// closed over the list item type) or the configuration's <see cref="SerginViewSlot.ListBody"/> component in its place.
/// </summary>
/// <remarks>
/// The view sends the overview query once per load (and again on reload, or after a row or bulk command that changed
/// records) and hands the response to both overview slots in <c>Context.Overview</c>; a slot never sends it itself. A
/// root listed under a parent (<c>ListUnder&lt;TParent&gt;</c>) takes the parent's id as <see cref="ParentId"/>: the view
/// loads the parent through the parent's model for the trail (the parent's list step, the parent, this list) and hands
/// both to the slots, and every list query it sends carries the id.
/// <para>
/// A task over the collection (<c>ITaskCommand&lt;TRoot&gt;</c>) is a header action after the create command: a click starts
/// it as one run with no record in the current scope (a second start while that run is going is refused, and the refusal,
/// which names the task and the scope, is shown as it comes). Its action carries a spinner while the activity indicator
/// lists its run in the current scope as unfinished, and stays clickable. A <c>Schedulable()</c> one is a split button
/// holding Schedule… and Schedules (its schedules in this scope, in <see cref="SerginTaskSchedulesDialog"/>) for a user who
/// may manage scheduled tasks. One with arguments asks for them in its edit mode: Dialog, the action opening
/// <see cref="TaskFormDialog"/>; Page, the action linking to its form page; Inline, a <see cref="GeneratedTaskSection"/>
/// above the table for a user who holds its permissions, and no action.
/// </para>
/// </remarks>
public sealed partial class SerginGeneratedListView<TRoot> : IDisposable
    where TRoot : class, IAggregateRoot
{
    private IGeneratedListBody? body;
    private Type? bodyType;
    private int generation;
    private object? overview;
    private AggregateViewModel? parentModel;
    private FieldView? parentTitleField;
    private object? parent;
    private Guid? loadedParentId;
    private bool canManageSchedules;
    private bool watchingActivity;
    private volatile bool disposed;
    private readonly HashSet<Type> starting = [];

    /// <summary>The parent's id of a root listed under a parent; required for one, ignored for any other.</summary>
    [Parameter]
    public Guid? ParentId { get; set; }

    [Inject]
    private AggregateViewRegistry Views { get; set; } = default!;

    [Inject]
    private ISerginDispatcher Dispatcher { get; set; } = default!;

    [Inject]
    private SerginUiModuleCatalog Catalog { get; set; } = default!;

    [Inject]
    private IUiErrorPresenter ErrorPresenter { get; set; } = default!;

    [Inject]
    private IDialogService DialogService { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = default!;

    [Inject]
    private ActivityPanelState ActivityPanel { get; set; } = default!;

    [Inject]
    private IScopeAuthorization ScopeAuthorization { get; set; } = default!;

    [Inject]
    private IAuthorizationScopeContext CurrentScope { get; set; } = default!;

    [Inject]
    private SerginScopeSession Scopes { get; set; } = default!;

    private AggregateViewModel Model { get; set; } = default!;

    // Re-read on every render, so the parent's step shows its record once loaded and its singular until then.
    private IReadOnlyList<SerginBreadcrumb> Trail => parentModel is null
        ? [GeneratedViewSupport.ListStep(Model, Catalog) ?? new SerginBreadcrumb(Model.Title)]
        : [.. ParentSteps(), new SerginBreadcrumb(Model.Title)];

    // Re-read on every render, so a collection task's spinner follows the activity indicator's rows. Internal, not private:
    // the task placement tests read the actions the bar is given.
    internal IReadOnlyList<SerginPageAction> Actions => [.. CreateAction(), .. CollectionTaskActions()];

    private IEnumerable<CommandView> CollectionTasks =>
        Model.Commands.Where(command => command is { Role: CommandRole.CollectionTask, Hidden: false });

    // A collection task with arguments in the Inline mode is a section of the list, not a header action.
    private static bool IsInlineSection(CommandView command) => command is { EditMode: SerginEditMode.Inline, TaskFields.Count: > 0 };

    // The inline sections this user may start (presentation only: the pipeline still checks).
    private IReadOnlyList<CommandView> inlineTasks = [];

    private RenderFragment? Overview => SlotOf(SerginViewSlot.ListOverview, keyed: true);

    private RenderFragment? OverviewSummary => SlotOf(SerginViewSlot.ListOverviewSummary, keyed: true);

    // The parent's id only for a root listed under one, so a stray ParentId on any other list reaches nothing.
    private Guid? ListParentId => parentModel is null ? null : ParentId;

    private AggregateViewContext Context =>
        new(Record: null, Version: null, ReloadAsync, [], overview, ListParentId, parent, generation);

    // A list body slot keeps its instance across reloads (it reloads on Context.Generation), unlike the overview slots.
    private RenderFragment Body => SlotOf(SerginViewSlot.ListBody, keyed: false) ?? (builder =>
    {
        builder.OpenComponent(0, bodyType!);
        builder.AddAttribute(1, nameof(GeneratedListBody<object>.Model), Model);
        builder.AddAttribute(2, nameof(GeneratedListBody<object>.ParentId), ListParentId);
        builder.AddAttribute(3, nameof(GeneratedListBody<object>.OnChanged), EventCallback.Factory.Create(this, RefreshSlotsAsync));
        builder.AddComponentReferenceCapture(4, component => body = (IGeneratedListBody)component);
        builder.CloseComponent();
    });

    protected override async Task OnInitializedAsync()
    {
        Model = Views.For<TRoot>();

        // A list route with no list query names a tree's page (the guard admits nothing else): that page is the tree's own.
        if (Model.List is null && Model.Routes.List is not null)
        {
            throw new InvalidOperationException(
                $"{typeof(TRoot).FullName} is read as a tree (ITreeQuery<{typeof(TRoot).Name}>), not listed: its list route is the "
                + "tree's page, which a generated list view does not render.");
        }

        if (!Model.Slots.ContainsKey(SerginViewSlot.ListBody))
        {
            bodyType = Model.ListBodyType ?? throw new InvalidOperationException(
                $"{typeof(TRoot).FullName} has no list query, so its list view has nothing to load. Add an IListOf<{typeof(TRoot).Name}>.");
        }

        if (Model.Parent is { } parentRoot)
        {
            parentModel = Views.Find(parentRoot)
                ?? throw new InvalidOperationException($"{typeof(TRoot).FullName} is listed under {parentRoot.FullName}, which has no view configuration.");
            parentTitleField = GeneratedViewSupport.TitleFieldOf(parentModel.DetailFields, parentModel.DetailTitleField);
        }

        // Only a list with a collection task has an action to mark busy. Subscribed before the first await, so a view
        // disposed while it initializes has already subscribed and unsubscribes.
        if (CollectionTasks.Any())
        {
            ActivityPanel.RowsChanged += OnActivityChanged;
            watchingActivity = true;
        }

        canManageSchedules = Model.HasSchedulableCollectionTask && await ScopeAuthorization.CurrentUserHasAsync(TaskPermissions.Manage);
        inlineTasks = [.. await HeldAsync(CollectionTasks.Where(IsInlineSection))];
        await LoadOverviewAsync();
    }

    private async Task<List<CommandView>> HeldAsync(IEnumerable<CommandView> candidates)
    {
        List<CommandView> held = [];

        foreach (CommandView command in candidates)
        {
            bool holds = true;

            foreach (PermissionDefinition permission in command.Permissions)
            {
                holds &= command.AnyScope
                    ? await ScopeAuthorization.CurrentUserHoldsAnywhereAsync(permission)
                    : await ScopeAuthorization.CurrentUserHasAsync(permission);
            }

            if (holds)
            {
                held.Add(command);
            }
        }

        return held;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        disposed = true;

        if (watchingActivity)
        {
            ActivityPanel.RowsChanged -= OnActivityChanged;
            watchingActivity = false;
        }
    }

    // Raised by the indicator, which must not be broken by a subscriber: a re-render is queued, never thrown into it.
    private void OnActivityChanged()
    {
        if (!disposed)
        {
            _ = RerenderAsync();
        }
    }

    private async Task RerenderAsync()
    {
        try
        {
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception exception) when (exception is ObjectDisposedException or InvalidOperationException)
        {
            // The renderer went away between the check and the call: nothing is left to draw.
        }
    }

    // The parent is read again whenever the route names another one; the list then reloads under it (see GeneratedListBody).
    protected override async Task OnParametersSetAsync()
    {
        if (parentModel is null)
        {
            return;
        }

        Guid parentId = ParentId ?? throw new InvalidOperationException(
            $"{typeof(TRoot).FullName} is listed under {parentModel.Root.FullName}, so its list view needs the parent's id: set ParentId.");

        if (loadedParentId == parentId)
        {
            return;
        }

        bool changed = loadedParentId is not null;
        loadedParentId = parentId;

        // Dropped first, so the trail never pairs the new parent's href with the old parent's name.
        parent = null;
        await LoadParentAsync(parentId);

        // The table reloads itself once it is handed the new ParentId; a list body slot sees the new generation.
        if (changed)
        {
            await RefreshSlotsAsync();
        }
    }

    // The parent's list step, then the parent linking to its detail: as stored once loaded, its singular until then.
    private IEnumerable<SerginBreadcrumb> ParentSteps()
    {
        AggregateViewModel model = parentModel!;

        if (GeneratedViewSupport.ListStep(model, Catalog) is { } list)
        {
            yield return list;
        }

        Guid parentId = ParentId.GetValueOrDefault();
        string? href = model.Routes.Detail?.Invoke(parentId);

        yield return parent is null
            ? new SerginBreadcrumb(model.Singular, href)
            : SerginBreadcrumb.Data(GeneratedViewSupport.DisplayOf(parent, parentTitleField, parentId), href);
    }

    // A failed read leaves no parent: the trail keeps its placeholder and the slots get null; the list still shows.
    private async Task LoadParentAsync(Guid parentId)
    {
        ViewSendResult loaded = await parentModel!.LoadRecordAsync(Dispatcher, parentId);

        parent = loaded.IsError ? null : loaded.Value;
    }

    // Once per load: both overview slots read the result from Context.Overview.
    private async Task LoadOverviewAsync() =>
        overview = Model.Overview is { } request && (Model.Slots.ContainsKey(SerginViewSlot.ListOverview) || Model.Slots.ContainsKey(SerginViewSlot.ListOverviewSummary))
            ? await GeneratedViewSupport.LoadOverviewAsync(Dispatcher, request.Invoker, request.Build())
            : null;

    // The create command, linking to the create route: its permission is the one the new record is checked on.
    private IEnumerable<SerginPageAction> CreateAction()
    {
        if (Model.Routes.Create is not { } create
            || Model.Commands.FirstOrDefault(command => command is { Role: CommandRole.Create, Hidden: false }) is not { } command)
        {
            yield break;
        }

        yield return new SerginPageAction(
            command.Label,
            command.Icon,
            SerginActionKind.Primary,
            Href: create,
            Permission: command.Permissions.FirstOrDefault(),
            AnyScope: command.AnyScope);
    }

    // Each shown collection task, starting it on click, busy while its run in the current scope is going.
    private IEnumerable<SerginPageAction> CollectionTaskActions()
    {
        foreach (CommandView command in CollectionTasks.Where(command => !IsInlineSection(command)))
        {
            List<SerginMenuItem> items = MenuItemsOf(command);

            yield return new SerginPageAction(
                command.Label,
                command.Icon,
                command.Kind,
                Href: command.PageRoute?.Invoke(null),
                OnClick: command.PageRoute is null ? EventCallback.Factory.Create(this, () => StartAsync(command)) : default,
                Permission: command.Permissions.FirstOrDefault(),
                AnyScope: command.AnyScope,
                Disabled: starting.Contains(command.Request),
                MenuItems: items.Count > 0 ? items : null,
                Busy: IsCollectionTaskRunning(command));
        }
    }

    private bool IsCollectionTaskRunning(CommandView command) =>
        command.Task is { } task && ActivityPanel.IsRunning(task.Name, targetId: null, CurrentScope.ScopeId);

    // Schedule… and Schedules for a schedulable collection task, to a user who may manage scheduled tasks.
    private List<SerginMenuItem> MenuItemsOf(CommandView command)
    {
        List<SerginMenuItem> items = [];

        if (command.Task is { Schedulable: true, Name: var name } && canManageSchedules)
        {
            items.Add(new SerginMenuItem(
                TaskUiTexts.ScheduleAction,
                Icons.Material.Outlined.Schedule,
                EventCallback.Factory.Create(this, () => DialogService.ShowScheduleAsync(ScheduleRequestFor(command))),
                command.Permissions.FirstOrDefault(),
                command.AnyScope));
            items.Add(new SerginMenuItem(
                TaskUiTexts.Schedules,
                Icons.Material.Outlined.EventNote,
                EventCallback.Factory.Create(this, () => ShowSchedulesAsync(name)),
                command.Permissions.FirstOrDefault(),
                command.AnyScope));
        }

        return items;
    }

    /// <summary>
    /// The Schedule dialog's request for a collection task: no targets, the current scope named instead, and the task's
    /// arguments at their declared defaults.
    /// </summary>
    internal SerginScheduleRequest ScheduleRequestFor(CommandView command)
    {
        ArgumentNullException.ThrowIfNull(command);

        GeneratedFormValues arguments = SerginScheduleArguments.Defaults(command);

        return new(
            Model.Schema,
            command.Label,
            () => GeneratedViewSupport.CollectionTemplateOf(command, arguments.Values),
            [],
            [TaskUiFormat.ScopeName(Scopes, CurrentScope.ScopeId)],
            TargetsCaption: TaskUiTexts.Dialog.Scope,
            TaskView: command,
            ArgumentValues: arguments);
    }

    /// <summary>Opens the collection task's schedules in the current scope. Internal for the task placement tests.</summary>
    internal Task<IDialogReference> ShowSchedulesAsync(string taskName) =>
        DialogService.ShowTaskSchedulesAsync(Model.Schema, taskName, CurrentScope.ScopeId);

    /// <summary>
    /// Starts a collection task in the current scope, its button disabled meanwhile: a refusal (another run of it going here)
    /// is shown as it comes; a start says so, and nothing is read again, since nothing has changed yet. Internal for the
    /// task placement tests.
    /// </summary>
    internal async Task StartAsync(CommandView command)
    {
        ArgumentNullException.ThrowIfNull(command);

        // One with arguments asks for them in its dialog, which starts it.
        if (command.TaskFields.Count > 0)
        {
            await AskTaskArgumentsAsync(command);
            return;
        }

        if (!starting.Add(command.Request))
        {
            return;
        }

        ViewSendResult result;

        try
        {
            result = await GeneratedViewSupport.StartCollectionAsync(Dispatcher, command);
        }
        finally
        {
            starting.Remove(command.Request);
        }

        if (result.IsError)
        {
            ErrorPresenter.Notify(result.Errors);
            return;
        }

        ActivityPanel.NotifyStarted(Snackbar, Localizer, Localizer.Text(ShellTexts.CommandActions.BackgroundStarted));
    }

    /// <summary>Opens a collection task's argument dialog; answers its reference. Internal for the argument form tests.</summary>
    internal Task<IDialogReference> AskTaskArgumentsAsync(CommandView command) => DialogService.ShowTaskFormAsync(command, []);

    private RenderFragment? SlotOf(SerginViewSlot slot, bool keyed)
    {
        if (Model.Slots.GetValueOrDefault(slot) is not { } component)
        {
            return null;
        }

        return builder =>
        {
            builder.OpenComponent(0, component);

            if (keyed)
            {
                builder.SetKey(generation);
            }

            builder.AddAttribute(1, "Context", Context);
            builder.CloseComponent();
        };
    }

    // A new key renders the overview slots anew, so each loads its figures again; a list body slot sees the new generation.
    private async Task RefreshSlotsAsync()
    {
        await LoadOverviewAsync();
        generation++;
    }

    private async Task ReloadAsync()
    {
        if (loadedParentId is { } parentId)
        {
            await LoadParentAsync(parentId);
        }

        await RefreshSlotsAsync();

        if (body is not null)
        {
            await body.ReloadAsync();
        }

        await InvokeAsync(StateHasChanged);
    }
}
