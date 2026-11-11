using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Application.ScheduledTasks;
using Sergin.SharedKernel.Application.ScheduledTasks.Commands;
using Sergin.SharedKernel.Application.ScheduledTasks.Queries;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Domain.ScheduledTasks;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Validation;

namespace Sergin.SharedKernel.Presentation.Blazor.ScheduledTasks;

/// <summary>
/// The Schedule dialog: the command's title, the module's own form for its non-target arguments
/// (<see cref="ArgumentsForm"/>), the records it will run on, a <see cref="SerginCronPicker"/>, and two buttons —
/// <b>Run now</b> sends the command once, saving nothing, and <b>Save</b> stores the schedule (or, with
/// <see cref="EditingId"/>, replaces the saved one at the version the dialog read when it opened).
/// </summary>
/// <remarks>
/// The form is validated the way every form is: field by field through <c>FormValidator.RulesFor(ToCommand)</c>, the
/// pipeline's own validator for <see cref="CreateScheduledTaskCommand"/> (or <see cref="UpdateScheduledTaskCommand"/>),
/// whose cron and zone rules the picker's custom field shows. A rule on a property with no field here (the template, the
/// targets) is the pipeline's, shown as snackbars on submit. The dialog closes with <c>true</c> when a schedule was saved.
/// </remarks>
public sealed partial class SerginScheduleDialog
{
    private readonly ScheduleFormModel model = new();

    private MudForm form = default!;
    private Func<object, string, Task<IEnumerable<string>>> validation = default!;
    private bool isValid = true;
    private bool busy;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    /// <summary>The module's schema; names the store the schedule belongs to.</summary>
    [Parameter]
    [EditorRequired]
    public string Schema { get; set; } = string.Empty;

    [Parameter]
    [EditorRequired]
    public TextDefinition CommandTitle { get; set; } = default!;

    /// <summary>Builds the command to schedule from the arguments the form holds, called on every validation and send.</summary>
    [Parameter]
    [EditorRequired]
    public Func<IBaseCommand> BuildTemplate { get; set; } = default!;

    /// <summary>The module's fields for the command's own arguments; none for a command that has none.</summary>
    [Parameter]
    public RenderFragment? ArgumentsForm { get; set; }

    [Parameter]
    [EditorRequired]
    public IReadOnlyCollection<ScheduledTaskTarget> Targets { get; set; } = [];

    /// <summary>What shows for each target, data as stored (an id, a name); one per target.</summary>
    [Parameter]
    public IReadOnlyList<string> TargetLabels { get; set; } = [];

    /// <summary>The saved schedule being changed; null to add a new one.</summary>
    [Parameter]
    public ScheduledTaskId? EditingId { get; set; }

    [Inject]
    private ISerginDispatcher Dispatcher { get; set; } = default!;

    [Inject]
    private ISerginFormValidator FormValidator { get; set; } = default!;

    [Inject]
    private IUiErrorPresenter ErrorPresenter { get; set; } = default!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    // The off-state line above Save: saving still works, but nothing will fire until the real engine is configured.
    [Inject]
    private IScheduledTaskEngine Engine { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        validation = EditingId is null
            ? FormValidator.RulesFor(ToCreate)
            : FormValidator.RulesFor(ToUpdate);

        if (EditingId is not null)
        {
            await ReloadAsync();
        }
    }

    /// <summary>The row version the dialog will send an edit at; null for a new schedule or before the saved one is read.</summary>
    internal RowVersion? Version { get; private set; }

    /// <summary>Reads the saved schedule again: its version, cron and zone become the form's. False when it could not be read.</summary>
    internal async Task<bool> ReloadAsync()
    {
        ErrorOr<Versioned<ScheduledTaskResponse>> loaded = await Dispatcher.SendVersionedAsync(new GetScheduledTaskByIdQuery(Schema, EditingId!));

        if (loaded.IsError)
        {
            ErrorPresenter.Notify(loaded.Errors);
            return false;
        }

        Version = loaded.Value.Version;
        model.Cron = loaded.Value.Value.Cron;
        model.TimeZoneId = loaded.Value.Value.TimeZoneId;

        return true;
    }

    private CreateScheduledTaskCommand ToCreate() =>
        new(Schema, BuildTemplate(), Targets, model.Cron, model.TimeZoneId);

    private UpdateScheduledTaskCommand ToUpdate() =>
        new(Schema, EditingId!, BuildTemplate(), Targets, model.Cron, model.TimeZoneId);

    private async Task RunNowAsync()
    {
        busy = true;

        try
        {
            ErrorOr<ScheduledRunStartedResponse> result =
                await Dispatcher.SendAsync(new RunCommandOnceCommand(Schema, BuildTemplate(), Targets));

            if (result.IsError)
            {
                ErrorPresenter.Notify(result.Errors);
                return;
            }

            Snackbar.Add(Localizer.Text(ScheduledTaskUiTexts.RunOnceStarted), Severity.Success);
            MudDialog.Close(DialogResult.Ok(false));
        }
        finally
        {
            busy = false;
        }
    }

    internal async Task SaveAsync()
    {
        await form.ValidateAsync();

        if (!form.IsValid)
        {
            return;
        }

        busy = true;

        try
        {
            ErrorOr<Success> saved = EditingId is null
                ? (await Dispatcher.SendAsync(ToCreate())).Then(_ => Result.Success)
                : (await Dispatcher.SendVersionedAsync(ToUpdate(), Version)).Then(_ => Result.Success);

            if (saved.IsError)
            {
                // Someone saved the schedule since this dialog read it: show what is stored now, with the new version, and say so.
                if (EditingId is not null && saved.Errors.Exists(VersionErrors.IsStale))
                {
                    if (await ReloadAsync())
                    {
                        Snackbar.Add(Localizer.Text(ScheduledTaskUiTexts.StaleReloaded), Severity.Warning);
                    }

                    return;
                }

                ErrorPresenter.Notify(saved.Errors);
                return;
            }

            Snackbar.Add(Localizer.Text(ScheduledTaskUiTexts.WasSaved), Severity.Success);
            MudDialog.Close(DialogResult.Ok(true));
        }
        finally
        {
            busy = false;
        }
    }

    private void Cancel() => MudDialog.Cancel();
}

/// <summary>The dialog's form model: the two properties of the command it has fields for, named as the command names them.</summary>
public sealed class ScheduleFormModel
{
    public string Cron { get; set; } = string.Empty;

    public string TimeZoneId { get; set; } = string.Empty;
}
