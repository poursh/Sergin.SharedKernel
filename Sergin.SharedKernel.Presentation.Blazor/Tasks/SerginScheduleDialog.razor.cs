using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Application.Tasks;
using Sergin.SharedKernel.Application.Tasks.Commands;
using Sergin.SharedKernel.Application.Tasks.Queries;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Domain.Tasks;
using Sergin.SharedKernel.Presentation.Blazor.Aggregates.Model;
using Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Validation;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Tasks;

/// <summary>
/// The Schedule dialog: the command's title with the record count (or, for a collection task, the scope), the task's
/// arguments (a generated task's <see cref="CommandView.TaskFields"/>, or a module page's own
/// <see cref="ArgumentsForm"/>), the records it will run on, a <see cref="SerginCronPicker"/>, and two buttons —
/// <b>Run now</b> sends the command once, saving nothing, and <b>Save</b> stores the schedule (or, with
/// <see cref="EditingId"/>, replaces the saved one at the version the dialog read when it opened).
/// </summary>
/// <remarks>
/// The form is validated the way every form is: field by field through <c>FormValidator.RulesFor(ToCommand)</c>, the
/// pipeline's own validator for <see cref="CreateTaskScheduleCommand"/> (or <see cref="UpdateTaskScheduleCommand"/>),
/// whose cron and zone rules the picker's custom field shows; a generated task's argument fields are checked by the task's
/// own validator over <see cref="BuildTemplate"/>, as its start's form checks them, so Save stays disabled while one is
/// invalid and the schedule stores what was entered. A rule on a property with no field here (the template, the
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
    public Func<ITaskCommand> BuildTemplate { get; set; } = default!;

    /// <summary>The module's fields for the command's own arguments; none for a command that has none.</summary>
    [Parameter]
    public RenderFragment? ArgumentsForm { get; set; }

    [Parameter]
    [EditorRequired]
    public IReadOnlyCollection<TaskScheduleTarget> Targets { get; set; } = [];

    /// <summary>What shows for each target, data as stored (an id, a name); one per target.</summary>
    [Parameter]
    public IReadOnlyList<string> TargetLabels { get; set; } = [];

    /// <summary>The caption above <see cref="TargetLabels"/>; null for "Records" (a collection task names its scope under "Scope").</summary>
    [Parameter]
    public TextDefinition? TargetsCaption { get; set; }

    /// <summary>A generated view's task, whose shown arguments render above the trigger; null for a module page's own form.</summary>
    [Parameter]
    public CommandView? TaskView { get; set; }

    /// <summary>With <see cref="TaskView"/>, the values its argument fields edit and <see cref="BuildTemplate"/> reads.</summary>
    [Parameter]
    public GeneratedFormValues? ArgumentValues { get; set; }

    /// <summary>The saved schedule being changed; null to add a new one.</summary>
    [Parameter]
    public TaskScheduleId? EditingId { get; set; }

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

    [Inject]
    private ICultureFormatter Formatter { get; set; } = default!;

    [Inject]
    private TaskCatalog Catalog { get; set; } = default!;

    // The off-state line above Save: saving still works, but nothing will fire until the real engine is configured.
    [Inject]
    private ITaskEngine Engine { get; set; } = default!;

    private IReadOnlyList<FormFieldView> ArgumentFields => TaskView is not null && ArgumentValues is not null ? TaskView.TaskFields : [];

    // "Read profile · 3 records" over records; "Refresh · North Region" for a collection task, which names its scope.
    private string TitleShown
    {
        get
        {
            string command = Localizer.Text(CommandTitle);

            return (Targets.Count, TargetLabels) switch
            {
                (1, _) => Localizer.Text(TaskUiTexts.Dialog.TitleOneRecord, new { Command = command }),
                ( > 1, _) => Localizer.Text(TaskUiTexts.Dialog.TitleRecords, new { Command = command, Count = Formatter.Number(Targets.Count) }),
                (0, [var scope]) => Localizer.Text(TaskUiTexts.Dialog.TitleInScope, new { Command = command, Scope = scope }),
                _ => command,
            };
        }
    }

    // An argument field's rule: the task's own validator over the task built from the values, as the start's form runs it.
    private async Task<IEnumerable<string>> ValidateArgumentAsync(string propertyName) =>
        await TaskView!.Invoker.ValidateAsync(FormValidator, BuildTemplate(), propertyName);

    // Internal for the schedule dialog tests: sets an argument as its input would, and re-renders so the input holds it.
    internal void SetArgument(string name, object? value)
    {
        ArgumentValues!.Set(name, value);
        StateHasChanged();
    }

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

    /// <summary>
    /// Reads the saved schedule again: its version, cron and zone become the form's, and so do its stored arguments for a
    /// generated task's fields. False when it could not be read.
    /// </summary>
    internal async Task<bool> ReloadAsync()
    {
        ErrorOr<Versioned<TaskScheduleResponse>> loaded = await Dispatcher.SendVersionedAsync(new GetTaskScheduleByIdQuery(Schema, EditingId!));

        if (loaded.IsError)
        {
            ErrorPresenter.Notify(loaded.Errors);
            return false;
        }

        TaskScheduleResponse saved = loaded.Value.Value;
        Version = loaded.Value.Version;
        model.Cron = saved.Cron;
        model.TimeZoneId = saved.TimeZoneId;

        // A NotShown argument has no field: the template gives it its declared default, the only value a form could set.
        if (ArgumentFields.Count > 0
            && Catalog.ByName(saved.CommandName) is { } command
            && SerginScheduleArguments.Rebuild(command, saved) is { IsError: false } built)
        {
            foreach ((string name, object? value) in SerginScheduleArguments.Of(TaskView!, built.Value).Values)
            {
                ArgumentValues!.Set(name, value);
            }
        }

        return true;
    }

    private CreateTaskScheduleCommand ToCreate() =>
        new(Schema, BuildTemplate(), Targets, model.Cron, model.TimeZoneId);

    private UpdateTaskScheduleCommand ToUpdate() =>
        new(Schema, EditingId!, BuildTemplate(), Targets, model.Cron, model.TimeZoneId);

    // Internal for the schedule dialog tests. The arguments are validated first, as Save validates them (the server checks
    // them again); a cron the picker shows as invalid does not matter to a run now, but the form validates it with the rest.
    internal async Task RunNowAsync()
    {
        await form.ValidateAsync();

        if (!form.IsValid)
        {
            return;
        }

        busy = true;

        try
        {
            ErrorOr<TaskScheduleRunStartedResponse> result =
                await Dispatcher.SendAsync(new RunTaskOnceCommand(Schema, BuildTemplate(), Targets));

            if (result.IsError)
            {
                ErrorPresenter.Notify(result.Errors);
                return;
            }

            Snackbar.Add(Localizer.Text(TaskUiTexts.RunOnceStarted), Severity.Success);
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
                        Snackbar.Add(Localizer.Text(TaskUiTexts.StaleReloaded), Severity.Warning);
                    }

                    return;
                }

                ErrorPresenter.Notify(saved.Errors);
                return;
            }

            Snackbar.Add(Localizer.Text(TaskUiTexts.WasSaved), Severity.Success);
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
