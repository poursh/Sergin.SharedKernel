using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Modules;
using Sergin.SharedKernel.Presentation.Blazor.Navigation;
using Sergin.SharedKernel.Presentation.Blazor.Pages;

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
/// </remarks>
public sealed partial class SerginGeneratedListView<TRoot>
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

    /// <summary>The parent's id of a root listed under a parent; required for one, ignored for any other.</summary>
    [Parameter]
    public Guid? ParentId { get; set; }

    [Inject]
    private AggregateViewRegistry Views { get; set; } = default!;

    [Inject]
    private ISerginDispatcher Dispatcher { get; set; } = default!;

    [Inject]
    private SerginUiModuleCatalog Catalog { get; set; } = default!;

    private AggregateViewModel Model { get; set; } = default!;

    // Re-read on every render, so the parent's step shows its record once loaded and its singular until then.
    private IReadOnlyList<SerginBreadcrumb> Trail => parentModel is null
        ? [GeneratedViewSupport.ListStep(Model, Catalog) ?? new SerginBreadcrumb(Model.Title)]
        : [.. ParentSteps(), new SerginBreadcrumb(Model.Title)];

    private IReadOnlyList<SerginPageAction> Actions { get; set; } = [];

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

        Actions = [.. CreateAction()];
        await LoadOverviewAsync();
    }

    // The parent is read again whenever the route names another one; the list then reloads under it.
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
        await LoadParentAsync(parentId);

        if (changed)
        {
            await RefreshSlotsAsync();

            if (body is not null)
            {
                await body.ReloadAsync();
            }
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
