using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Domain.Securities;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// One entry of the menu behind a <see cref="SerginPageAction"/>'s split button — a further command on the same
/// subject, such as scheduling the one the button runs.
/// </summary>
/// <param name="Label">The entry text, a declared text rendered in the current UI culture.</param>
/// <param name="Icon">A MudBlazor icon constant; null for none.</param>
/// <param name="OnClick">What a click runs; create it with <c>EventCallback.Factory.Create(this, …)</c>.</param>
/// <param name="Permission">The permission the entry needs; hidden from a user who lacks it — presentation only.</param>
/// <param name="AnyScope">Whether the permission is checked on any scope rather than the current one.</param>
/// <param name="LabelArguments">Fills the label's placeholders (<c>{Count}</c>) through <c>Localizer.Text(label, args)</c>.</param>
public sealed record SerginMenuItem(
    TextDefinition Label,
    string? Icon,
    EventCallback OnClick,
    PermissionDefinition? Permission = null,
    bool AnyScope = false,
    object? LabelArguments = null);

/// <summary>One entry of the menu behind a <see cref="SerginBulkAction{TItem}"/>'s split button.</summary>
/// <param name="Label">The entry text, a declared text rendered in the current UI culture.</param>
/// <param name="Icon">A MudBlazor icon constant; null for none.</param>
/// <param name="OnRun">What a click runs, given the selected rows.</param>
/// <param name="Permission">The permission the entry needs; hidden from a user who lacks it — presentation only.</param>
/// <param name="AnyScope">Whether the permission is checked on any scope rather than the current one.</param>
public sealed record SerginBulkMenuItem<TItem>(
    TextDefinition Label,
    string? Icon,
    Func<IReadOnlyList<TItem>, Task> OnRun,
    PermissionDefinition? Permission = null,
    bool AnyScope = false);
