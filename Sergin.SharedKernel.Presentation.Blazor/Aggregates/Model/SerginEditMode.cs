namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>Where a command that edits a record is offered: in a dialog, in place, or on a page of its own.</summary>
public enum SerginEditMode
{
    Dialog,
    Inline,
    Page,
}
