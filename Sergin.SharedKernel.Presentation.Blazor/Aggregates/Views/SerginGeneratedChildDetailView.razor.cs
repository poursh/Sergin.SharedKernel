using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Modules;
using Sergin.SharedKernel.Presentation.Blazor.Navigation;
using Sergin.SharedKernel.Presentation.Blazor.Pages;
using Sergin.SharedKernel.Presentation.Blazor.Views;
using Sergin.SharedKernel.Presentation.Errors;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views;

/// <summary>
/// The detail view of <typeparamref name="TChild"/>, a child entity of <typeparamref name="TRoot"/>, generated from the
/// root's <see cref="AggregateViewModel"/> and the child's <see cref="ChildViewModel"/>: the child loaded through its
/// GetOne query, a field per visible response property, the edit command as a link to the child's edit route, and each
/// of its remove and transition commands as an action.
/// </summary>
/// <remarks>
/// The root is read first and keeps its version: a child command changes the root, which is what is guarded, so it is
/// sent at that version. A change to the child between the two reads makes the version stale, so the command is refused
/// instead of acting on a child this view shows as it was. A root that failed to load while the child loaded leaves no
/// version; a guarded command then reports not found and reads again instead of sending a fabricated one. A remove is
/// confirmed, sent at the root's version and leaves for the root's detail page; a stale answer reads both again.
/// </remarks>
public sealed partial class SerginGeneratedChildDetailView<TRoot, TChild>
    where TRoot : class, IAggregateRoot
    where TChild : class
{
    private readonly HashSet<Type> busy = [];
    private object? record;
    private object? rootRecord;
    private RowVersion? version;
    private SerginProblem? problem;
    private FieldView? titleField;
    private FieldView? rootTitleField;

    [Parameter]
    public Guid RootId { get; set; }

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

    [CascadingParameter]
    private SerginViewHost Host { get; set; } = SerginViewHost.Unhosted;

    private AggregateViewModel Model { get; set; } = default!;

    private ChildViewModel Child { get; set; } = default!;

    private IEnumerable<FieldView> VisibleFields => Child.DetailFields.Where(candidate => !candidate.Hidden);

    private AggregateViewContext Context => new(record, version, ReloadAsync, [RootId, Id], null);

    private string DisplayText => GeneratedViewSupport.DisplayOf(record, titleField, Id);

    // The list step, then the root (linked by the route's RootId, so it is a link before the load and beside the
    // problem panel), then the child: each as stored once loaded, the type's name until then.
    private IReadOnlyList<SerginBreadcrumb> Trail =>
    [
        .. ListStep(),
        RootStep(),
        record is null ? new(Child.Singular) : SerginBreadcrumb.Data(DisplayText),
    ];

    private IReadOnlyList<SerginPageAction> Actions => record is null ? [] : [.. ActionsOf()];

    protected override void OnInitialized()
    {
        Model = Views.For<TRoot>();
        Child = Model.Children.FirstOrDefault(candidate => candidate.Child == typeof(TChild))
            ?? throw new InvalidOperationException($"{typeof(TRoot).FullName} declares no view for the child {typeof(TChild).FullName}.");

        if (Child.GetOne is null || Model.GetOne is null || Model.Routes.Detail is null)
        {
            throw new InvalidOperationException(
                $"{typeof(TChild).FullName} has no GetOne query, or {typeof(TRoot).FullName} no detail route, so the child's detail view has nothing to load or link to.");
        }

        titleField = GeneratedViewSupport.TitleFieldOf(Child.DetailFields, Child.DetailTitleField);
        rootTitleField = GeneratedViewSupport.TitleFieldOf(Model.DetailFields, Model.DetailTitleField);
    }

    protected override Task OnParametersSetAsync() => LoadAsync();

    private IEnumerable<SerginBreadcrumb> ListStep() =>
        GeneratedViewSupport.ListStep(Model, Catalog) is { } step ? [step] : [];

    private SerginBreadcrumb RootStep()
    {
        string href = Model.Routes.Detail!(RootId);

        return rootRecord is null
            ? new SerginBreadcrumb(Model.Singular, href)
            : SerginBreadcrumb.Data(GeneratedViewSupport.DisplayOf(rootRecord, rootTitleField, RootId), href);
    }

    // The root's version is read before the child, so a change to the child between the two reads makes it stale.
    private async Task LoadAsync()
    {
        ViewSendResult root = await Model.LoadRecordAsync(Dispatcher, RootId);
        ViewSendResult loaded = await Child.GetOne!.Invoker.SendAsync(Dispatcher, Child.GetOne.Build(RootId, Id));

        // A failed root read leaves no root name and no version; the child's page still shows.
        rootRecord = root.IsError ? null : root.Value;
        version = root.IsError ? null : root.Version;

        if (loaded.IsError)
        {
            record = null;
            version = null;
            problem = ErrorPresenter.Present(loaded.Errors[0]);

            return;
        }

        problem = null;
        record = loaded.Value;
    }

    private async Task ReloadAsync()
    {
        await LoadAsync();
        await InvokeAsync(StateHasChanged);
    }

    // The edit command links to the child's edit route; each remove and transition runs through this view.
    private IEnumerable<SerginPageAction> ActionsOf()
    {
        if (Child.EditCommand is { Hidden: false, EditMode: SerginEditMode.Page } edit && Child.Routes.Edit is { } editRoute)
        {
            yield return Action(edit) with { Href = editRoute(RootId, Id) };
        }

        foreach (CommandView command in Child.Commands.Where(command => command is
                 { Role: CommandRole.RemoveChild or CommandRole.Transition or CommandRole.SetProperty, Hidden: false, BuildFromIds: not null }))
        {
            yield return Action(command) with
            {
                OnClick = EventCallback.Factory.Create(this, () => command.Role == CommandRole.RemoveChild ? RemoveAsync(command) : RunAsync(command)),
            };
        }
    }

    private SerginPageAction Action(CommandView command)
    {
        // The detail page has no tooltip on a disabled action, so a command's DisabledReason shows on rows only.
        bool refused = command.DisabledWhen?.Invoke(record!) == true;

        return new SerginPageAction(
            command.Label,
            command.Icon,
            command.Kind,
            Permission: command.Permissions.FirstOrDefault(),
            AnyScope: command.AnyScope,
            Disabled: refused || busy.Contains(command.Request));
    }

    // A guarded command needs the root's version; without one it reports not found and reads again, as a stale answer would.
    private async Task<ViewSendResult?> SendAsync(CommandView command)
    {
        if (command.RequiresVersion && version is null)
        {
            ErrorPresenter.Notify(Error.NotFound());
            await LoadAsync();

            return null;
        }

        busy.Add(command.Request);

        try
        {
            return await GeneratedChildBulk.SendAsync(Dispatcher, command, RootId, Id, version);
        }
        finally
        {
            busy.Remove(command.Request);
        }
    }

    private async Task RunAsync(CommandView command)
    {
        if (await SendAsync(command) is not { } result || await GeneratedViewSupport.FailedAsync(result, ErrorPresenter, LoadAsync))
        {
            return;
        }

        if (command.SucceededText is { } succeeded)
        {
            Snackbar.Add(Localizer.Text(succeeded, PlaceholderArguments()), Severity.Success);
        }

        await LoadAsync();
    }

    private async Task RemoveAsync(CommandView command)
    {
        if (!await DialogService.ConfirmDeleteAsync(
                Localizer, command.ConfirmTitle ?? CommonTexts.Delete, command.ConfirmText ?? ShellTexts.CommandActions.DeleteConfirm, PlaceholderArguments(), command.Label))
        {
            return;
        }

        // An in-use child arrives as one validation error; a stale root as a version error, which reads both again.
        if (await SendAsync(command) is not { } result || await GeneratedViewSupport.FailedAsync(result, ErrorPresenter, LoadAsync))
        {
            return;
        }

        await Host.LeaveAsync(Model.Routes.Detail!(RootId));
    }

    private Dictionary<string, object?> PlaceholderArguments() =>
        GeneratedViewSupport.ArgumentsOf(Child.DetailArguments, record, DisplayText);
}
