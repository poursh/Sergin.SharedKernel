using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Modules;
using Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;
using Sergin.SharedKernel.Presentation.Blazor.Modules;
using Sergin.SharedKernel.Presentation.Blazor.Navigation;
using Sergin.SharedKernel.Presentation.Blazor.Pages;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views;

/// <summary>
/// The list view of <typeparamref name="TRoot"/>, generated from its <see cref="AggregateViewModel"/>: the list's title,
/// its nav entry as the trail, a reload button, the configuration's <see cref="SerginViewSlot.ListOverview"/> and
/// <see cref="SerginViewSlot.ListOverviewSummary"/> components (given <c>Context</c>, an <see cref="AggregateViewContext"/>
/// with no record), the create command as the primary action, and the table (<see cref="GeneratedListBody{TItem}"/>,
/// closed over the list item type).
/// </summary>
/// <remarks>
/// Reload reads the table again and renders the overview slots anew, so they load again; so does a row or bulk command
/// that changed records.
/// </remarks>
public sealed partial class SerginGeneratedListView<TRoot>
    where TRoot : class, IAggregateRoot
{
    private IGeneratedListBody? body;
    private Type bodyType = default!;
    private int generation;

    [Inject]
    private AggregateViewRegistry Views { get; set; } = default!;

    [Inject]
    private SerginUiModuleCatalog Catalog { get; set; } = default!;

    private AggregateViewModel Model { get; set; } = default!;

    private IReadOnlyList<SerginBreadcrumb> Trail { get; set; } = [];

    private IReadOnlyList<SerginPageAction> Actions { get; set; } = [];

    private RenderFragment? Overview => SlotOf(SerginViewSlot.ListOverview);

    private RenderFragment? OverviewSummary => SlotOf(SerginViewSlot.ListOverviewSummary);

    private AggregateViewContext Context => new(Record: null, Version: null, ReloadAsync, []);

    private RenderFragment Body => builder =>
    {
        builder.OpenComponent(0, bodyType);
        builder.AddAttribute(1, nameof(GeneratedListBody<object>.Model), Model);
        builder.AddAttribute(2, nameof(GeneratedListBody<object>.OnChanged), EventCallback.Factory.Create(this, RefreshSlots));
        builder.AddComponentReferenceCapture(3, component => body = (IGeneratedListBody)component);
        builder.CloseComponent();
    };

    protected override void OnInitialized()
    {
        Model = Views.For<TRoot>();

        Type item = Model.List?.GetInterfaces()
                .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IListQuery<>))
                .Select(contract => contract.GetGenericArguments()[0])
                .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"{typeof(TRoot).FullName} has no list query, so its list view has nothing to load. Add an IListOf<{typeof(TRoot).Name}>.");

        bodyType = GeneratedLists.BodyTypeOf(item);
        Trail = [ListStep()];
        Actions = [.. CreateAction()];
    }

    private SerginBreadcrumb ListStep()
    {
        if (Model.Routes.List is not { } list)
        {
            return new SerginBreadcrumb(Model.Title);
        }

        SerginNavItem? item = Catalog.NavItems.FirstOrDefault(candidate => string.Equals(candidate.Href, list, StringComparison.OrdinalIgnoreCase));

        return item is null ? new SerginBreadcrumb(Model.Title, list) : SerginBreadcrumb.Of(item);
    }

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

    private RenderFragment? SlotOf(SerginViewSlot slot)
    {
        if (Model.Slots.GetValueOrDefault(slot) is not { } component)
        {
            return null;
        }

        return builder =>
        {
            builder.OpenComponent(0, component);
            builder.SetKey(generation);
            builder.AddAttribute(1, "Context", Context);
            builder.CloseComponent();
        };
    }

    // A new key renders the slots anew, so each loads its figures again.
    private void RefreshSlots() => generation++;

    private async Task ReloadAsync()
    {
        RefreshSlots();

        if (body is not null)
        {
            await body.ReloadAsync();
        }

        await InvokeAsync(StateHasChanged);
    }
}
