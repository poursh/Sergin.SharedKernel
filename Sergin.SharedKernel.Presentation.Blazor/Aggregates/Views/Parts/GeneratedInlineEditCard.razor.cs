using Microsoft.AspNetCore.Components;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

/// <summary>
/// A root's set-property command declared <c>Edit(SerginEditMode.Inline)</c> whose value is not a choice: its
/// <see cref="GeneratedValueForm"/> in an outlined card, the submit labelled by the command (Rename). The detail view
/// renders it only for a user who holds the command's permissions.
/// </summary>
public sealed partial class GeneratedInlineEditCard
{
    [Parameter]
    [EditorRequired]
    public CommandView Command { get; set; } = default!;

    [Parameter]
    public Guid Id { get; set; }

    /// <summary>The record's current value.</summary>
    [Parameter]
    public object? Value { get; set; }

    [Parameter]
    public EventCallback<object?> OnSubmit { get; set; }

    [Parameter]
    public bool Busy { get; set; }
}
