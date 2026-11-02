using Microsoft.AspNetCore.Components;

namespace Sergin.SharedKernel.Presentation.Blazor.Views;

/// <summary>
/// The layout a routable page gives its view: <c>@page "/dm/devices/{Id:guid}"</c>, <c>@layout SerginPageLayout</c>,
/// then <c>&lt;DeviceDetailView Id="Id" /&gt;</c>.
/// </summary>
/// <remarks>
/// It nests in <c>SerginMainLayout</c> and cascades a <see cref="SerginViewHostKind.Page"/> <see cref="SerginViewHost"/>
/// to the page, which tells the view to render the page title and the header row (trail and header buttons) into the
/// shell, and makes leaving a navigation.
/// </remarks>
public sealed partial class SerginPageLayout
{
    private SerginViewHost host = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    protected override void OnInitialized() => host = new SerginViewHost(SerginViewHostKind.Page, Leave);

    private Task Leave(string href)
    {
        Navigation.NavigateTo(href);

        return Task.CompletedTask;
    }
}
