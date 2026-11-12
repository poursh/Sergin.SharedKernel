using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

/// <summary>
/// The fields of a generated list's filter panel, bound to the list body's <see cref="GeneratedFilterState"/>. A
/// configured filter component gets <c>Value</c> and <c>ValueChanged</c>, and its instance is kept so the chip can ask
/// it to name the value (<see cref="IListFilterLabel"/>).
/// </summary>
public sealed partial class GeneratedFilterPanel
{
    [Parameter]
    [EditorRequired]
    public GeneratedFilterState State { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    [Inject]
    private ICultureFormatter Formatter { get; set; } = default!;

    private Task OnValueChangedAsync(string id, Guid? value) => State.SetValueAsync(id, value);

    private RenderFragment ConfiguredFilter(ListFilterView filter) => builder =>
    {
        string id = filter.Field.Id;

        builder.OpenComponent(0, filter.Component!);
        builder.AddAttribute(1, "Value", State.ValueOf(id));
        builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<Guid?>(this, value => OnValueChangedAsync(id, value)));
        builder.AddComponentReferenceCapture(3, component => State.Capture(id, component));
        builder.CloseComponent();
    };
}
