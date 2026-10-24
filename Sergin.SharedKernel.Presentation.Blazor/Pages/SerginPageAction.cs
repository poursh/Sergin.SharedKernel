using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Domain.Securities;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>How an action is drawn and where it sits in the actions bar.</summary>
public enum SerginActionKind
{
    /// <summary>The page's main action — a filled button, first.</summary>
    Primary,

    /// <summary>An ordinary action — an outlined button, after the primary one.</summary>
    Secondary,

    /// <summary>A destructive action — a text button in the error colour, pushed to the far end of the bar.</summary>
    Danger,
}

/// <summary>
/// One button of a <see cref="SerginAggregatePage"/>'s actions bar. Either <see cref="Href"/> or
/// <see cref="OnClick"/> says what it does.
/// </summary>
/// <param name="Label">The button text, localised by the bar.</param>
/// <param name="Icon">A MudBlazor icon constant; null for none.</param>
/// <param name="Kind">Its look and place.</param>
/// <param name="Href">A page to navigate to.</param>
/// <param name="OnClick">What a click runs; create it with <c>EventCallback.Factory.Create(this, …)</c> so the page re-renders after it.</param>
/// <param name="Permission">
/// The permission the action needs. The bar hides an action the user does not hold it for — presentation only: the
/// handler behind it enforces the same permission whether or not the button showed.
/// </param>
/// <param name="AnyScope">
/// Whether the permission is checked on any scope rather than the current one — true for an action on an aggregate
/// that is not <c>Scoped()</c>, mirroring how the pipeline checks a configuration bound to it.
/// </param>
/// <param name="Disabled">Shown but not clickable, e.g. while a delete is running.</param>
public sealed record SerginPageAction(
    string Label,
    string? Icon,
    SerginActionKind Kind,
    string? Href = null,
    EventCallback OnClick = default,
    PermissionDefinition? Permission = null,
    bool AnyScope = false,
    bool Disabled = false);
