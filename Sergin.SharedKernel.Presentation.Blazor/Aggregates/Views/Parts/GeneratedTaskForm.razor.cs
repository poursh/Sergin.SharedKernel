using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Tasks.Commands;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Tasks;
using Sergin.SharedKernel.Presentation.Blazor.Validation;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

/// <summary>
/// The argument form of a task (<see cref="CommandView.TaskFields"/>), the body of its dialog and of its inline section:
/// each argument checked field by field by the pipeline's own validator, and Start, which validates first and then starts
/// the task with the values over <see cref="Targets"/> (none for a task over the collection). A refusal is shown and the
/// form stays as it was; a start says so and raises <see cref="OnStarted"/>. The values are kept either way, so an inline
/// form can start the same task again.
/// </summary>
public sealed partial class GeneratedTaskForm
{
    private MudForm form = default!;
    private bool isValid = true;
    private bool starting;
    private Func<object, string, Task<IEnumerable<string>>> validation = default!;

    /// <summary>A shown task: <see cref="CommandRole.Task"/> or <see cref="CommandRole.CollectionTask"/>.</summary>
    [Parameter]
    [EditorRequired]
    public CommandView Command { get; set; } = default!;

    /// <summary>The records the task is started over, each with its title as the person saw it; empty for a task over the collection.</summary>
    [Parameter]
    public IReadOnlyCollection<TaskTarget> Targets { get; set; } = [];

    /// <summary>Raised once the run started; create it on the host so the host re-renders after it.</summary>
    [Parameter]
    public EventCallback OnStarted { get; set; }

    /// <summary>True while the task's run on the record is going: the spinner shows. Start stays enabled.</summary>
    [Parameter]
    public bool Busy { get; set; }

    /// <summary>Further buttons after Start (a dialog's Cancel).</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Inject]
    private ISerginFormValidator FormValidator { get; set; } = default!;

    [Inject]
    private ISerginDispatcher Dispatcher { get; set; } = default!;

    [Inject]
    private IUiErrorPresenter ErrorPresenter { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    [Inject]
    private ICultureFormatter Formatter { get; set; } = default!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = default!;

    [Inject]
    private ActivityPanelState ActivityPanel { get; set; } = default!;

    // Internal, not private: the argument form tests set the values the inputs would.
    internal GeneratedTaskArguments Arguments { get; private set; } = default!;

    // An EventCallback, not a bare delegate: splatted onto MudForm's <form> it gets a receiver, so the form re-renders.
    private EventCallback OnFormSubmit => EventCallback.Factory.Create(this, SubmitAsync);

    private Guid? FirstTargetId => Targets.FirstOrDefault()?.TargetId;

    protected override void OnInitialized()
    {
        Arguments = new GeneratedTaskArguments(Command, FormValidator);

        // MudForm's form-level rule, for an input with a For: the field by its path.
        validation = async (_, propertyName) => await ValidateAsync(propertyName);
    }

    private Task<IEnumerable<string>> ValidateAsync(string propertyName) => Arguments.ValidateAsync(propertyName, FirstTargetId);

    // Internal for the argument form tests: sets a value as its input would, and re-renders so the input holds it.
    internal void SetValue(string name, object? value)
    {
        Arguments.Values.Set(name, value);
        StateHasChanged();
    }

    // Internal for the argument form tests, which submit as Start and Enter do.
    internal async Task SubmitAsync()
    {
        await form.ValidateAsync();

        if (!form.IsValid || starting)
        {
            return;
        }

        starting = true;
        ViewSendResult result;

        try
        {
            result = await Arguments.StartAsync(Dispatcher, Targets);
        }
        finally
        {
            starting = false;
        }

        if (result.IsError)
        {
            ErrorPresenter.Notify(result.Errors);
            return;
        }

        GeneratedTaskStarts.Notify(ActivityPanel, Snackbar, Localizer, Formatter, Targets.Count);
        await OnStarted.InvokeAsync();
    }
}

/// <summary>What every task start says once its run started.</summary>
internal static class GeneratedTaskStarts
{
    /// <summary>The started snackbar, naming the count when the run is over more than one record.</summary>
    public static void Notify(ActivityPanelState activity, ISnackbar snackbar, IStringLocalizer localizer, ICultureFormatter formatter, int targets) =>
        activity.NotifyStarted(
            snackbar,
            localizer,
            targets > 1
                ? localizer.Text(ShellTexts.CommandActions.BackgroundStartedMany, new { Count = formatter.Number(targets) })
                : localizer.Text(ShellTexts.CommandActions.BackgroundStarted));
}
