using MudBlazor;

namespace Sergin.SharedKernel.Presentation.Blazor.ScheduledTasks;

/// <summary>
/// The circuit's way to open the app bar's activity popover from elsewhere: the "Show" action of a started snackbar.
/// Scoped, so per circuit; the indicator listens while it is rendered.
/// </summary>
internal sealed class ActivityPanelState
{
    public event Func<Task>? OpenRequested;

    public Task OpenAsync() => OpenRequested?.Invoke() ?? Task.CompletedTask;

    /// <summary>The snackbar a background start answers with, its "Show" action opening the activity popover.</summary>
    public void NotifyStarted(ISnackbar snackbar, IStringLocalizer localizer, string message)
    {
        ArgumentNullException.ThrowIfNull(snackbar);
        ArgumentNullException.ThrowIfNull(localizer);

        snackbar.Add(message, Severity.Info, options =>
        {
            options.Action = localizer.Text(ScheduledTaskUiTexts.Activity.Show);
            options.OnClick = _ => OpenAsync();
        });
    }
}
