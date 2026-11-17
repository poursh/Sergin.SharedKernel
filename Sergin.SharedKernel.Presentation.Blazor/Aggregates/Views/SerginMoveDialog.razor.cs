using System.Collections;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Errors;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views;

/// <summary>
/// A root's move command in a dialog: the root's tree, read when the dialog opens (not with the record), offered as a select
/// of the parents the record may move under (<see cref="MoveCandidates"/>: not itself, nor anything below it), indented by
/// depth and labelled with the move command's <c>NewParentId</c> field text. Nothing is picked at first, and the confirm
/// button, the move command's label, stays disabled until something is. Confirm closes the dialog with the picked parent's
/// <see cref="Guid"/>; Cancel closes it cancelled. A tree that cannot be read shows its problem in the dialog and offers nothing.
/// </summary>
public sealed partial class SerginMoveDialog
{
    private const int IndentPixels = 16;

    private IReadOnlyList<(Guid Id, string Title, int Depth)>? candidates;
    private Guid? selected;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    /// <summary>The root's tree.</summary>
    [Parameter]
    [EditorRequired]
    public TreeView Tree { get; set; } = default!;

    /// <summary>The record being moved: it and its descendants are not offered.</summary>
    [Parameter]
    public Guid Id { get; set; }

    /// <summary>The picker's label, the move command's <c>NewParentId</c> field text.</summary>
    [Parameter]
    [EditorRequired]
    public FieldText Field { get; set; } = default!;

    /// <summary>The confirm button's text, the move command's label.</summary>
    [Parameter]
    [EditorRequired]
    public TextDefinition ConfirmLabel { get; set; } = default!;

    [Inject]
    private ISerginDispatcher Dispatcher { get; set; } = default!;

    [Inject]
    private IUiErrorPresenter ErrorPresenter { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    // Internal, not private: the move test reads the offered parents and drives Pick, Confirm and Cancel, as the select and buttons do.
    internal IReadOnlyList<(Guid Id, string Title, int Depth)> Candidates => candidates ?? [];

    internal bool Loaded => candidates is not null && Problem is null;

    // Internal: the move test reads the problem a failed tree read shows.
    internal SerginProblem? Problem { get; private set; }

    internal bool CanConfirm => selected is not null;

    private string? PlaceholderShown => Field.Placeholder is { } placeholder ? Localizer.Text(placeholder) : null;

    private string? HintShown => Field.Hint is { } hint ? Localizer.Text(hint) : null;

    protected override async Task OnInitializedAsync()
    {
        ViewSendResult loaded = await Tree.Query.Invoker.SendAsync(Dispatcher, Tree.Query.Build());

        if (loaded.IsError)
        {
            Problem = ErrorPresenter.Present(loaded.Errors[0]);
            candidates = [];

            return;
        }

        IReadOnlyCollection<object> items = [.. ((IEnumerable)loaded.Value!).Cast<object>()];
        candidates = MoveCandidates.For(Tree, items, Id);
    }

    internal void Pick(Guid? parentId) => selected = parentId;

    internal void Confirm()
    {
        if (selected is { } parentId)
        {
            MudDialog.Close(DialogResult.Ok(parentId));
        }
    }

    internal void Cancel() => MudDialog.Cancel();

    private static string IndentOf(int depth) =>
        string.Create(CultureInfo.InvariantCulture, $"padding-inline-start: {depth * IndentPixels}px");

    private string TitleOf(Guid? parentId) =>
        parentId is { } id && Candidates.FirstOrDefault(candidate => candidate.Id == id) is { Title: { } title } ? title : string.Empty;
}
