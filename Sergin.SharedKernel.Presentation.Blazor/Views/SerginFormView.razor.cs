using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Presentation.Blazor.Navigation;
using Sergin.SharedKernel.Presentation.Errors;

namespace Sergin.SharedKernel.Presentation.Blazor.Views;

/// <summary>
/// A create or edit form as a view: the title as heading, a <see cref="MudForm"/> validated field by field by the
/// pipeline's own validator (<see cref="Validation"/>, from <c>ISerginFormValidator.RulesFor(ToCommand)</c>), the
/// fields (<see cref="ChildContent"/>), then the submit and cancel buttons.
/// </summary>
/// <remarks>
/// It owns the form: on submit it validates every field and invokes <see cref="OnSubmit"/> only when all pass, so the
/// module view's handler starts at the dispatch. The module view keeps <see cref="Submitting"/>, maps the form model
/// to its command once (<c>ToCommand</c>, used for both validation and submit), and when the save succeeds calls
/// <see cref="SerginViewHost.LeaveAsync"/>. Cancel leaves for <see cref="CancelHref"/>.
/// </remarks>
public sealed partial class SerginFormView
{
    private MudForm form = default!;
    private bool isValid;

    /// <summary>The page title on a page, and the form's heading.</summary>
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>The view's steps below home, first to last; shown only on a page.</summary>
    [Parameter]
    public IReadOnlyList<SerginBreadcrumb> Trail { get; set; } = [];

    /// <summary>What went wrong loading the record to edit; shown above the form.</summary>
    [Parameter]
    public SerginProblem? Problem { get; set; }

    /// <summary>Whether the form renders; an edit form waits for the record and its version.</summary>
    [Parameter]
    public bool Ready { get; set; } = true;

    /// <summary>The form model the fields bind to.</summary>
    [Parameter]
    [EditorRequired]
    public object Model { get; set; } = default!;

    /// <summary>The field validation: <c>FormValidator.RulesFor(ToCommand)</c>.</summary>
    [Parameter]
    [EditorRequired]
    public Func<object, string, Task<IEnumerable<string>>> Validation { get; set; } = default!;

    /// <summary>Runs once every field is valid; create it on the module view so the view re-renders after it.</summary>
    [Parameter]
    public EventCallback OnSubmit { get; set; }

    /// <summary>True while the module view's save runs; disables the submit button.</summary>
    [Parameter]
    public bool Submitting { get; set; }

    [Parameter]
    public string SubmitLabel { get; set; } = "Save";

    /// <summary>Where cancel leaves for.</summary>
    [Parameter]
    [EditorRequired]
    public string CancelHref { get; set; } = string.Empty;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [CascadingParameter]
    private SerginViewHost Host { get; set; } = SerginViewHost.Unhosted;

    // An EventCallback, not a bare delegate: splatted onto MudForm's <form> it gets a receiver, so the view
    // re-renders after SubmitAsync.
    private EventCallback OnFormSubmit => EventCallback.Factory.Create(this, SubmitAsync);

    private async Task SubmitAsync()
    {
        await form.ValidateAsync();

        if (!form.IsValid)
        {
            return;
        }

        await OnSubmit.InvokeAsync();
    }

    private Task CancelAsync() => Host.LeaveAsync(CancelHref);
}
