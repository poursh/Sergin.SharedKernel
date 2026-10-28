using Microsoft.AspNetCore.Components;

namespace Sergin.SharedKernel.Presentation.Blazor.Views;

/// <summary>
/// The host a routable page gives its view: <c>@page "/dm/devices/{Id:guid}"</c> then
/// <c>&lt;SerginPage&gt;&lt;DeviceDetailView Id="Id" /&gt;&lt;/SerginPage&gt;</c>.
/// </summary>
/// <remarks>
/// It cascades a <see cref="SerginViewHostKind.Page"/> <see cref="SerginViewHost"/>, which tells the view to render
/// the page title and the header row (trail and header buttons) into the layout, and makes leaving a navigation.
/// </remarks>
public sealed partial class SerginPage
{
    private SerginViewHost host = default!;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    protected override void OnInitialized() => host = new SerginViewHost(SerginViewHostKind.Page, Leave);

    private Task Leave(string href)
    {
        Navigation.NavigateTo(href);

        return Task.CompletedTask;
    }
}
