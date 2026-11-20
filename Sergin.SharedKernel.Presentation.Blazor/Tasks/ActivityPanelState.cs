using MudBlazor;

namespace Sergin.SharedKernel.Presentation.Blazor.Tasks;

/// <summary>
/// The circuit's way to open the app bar's activity popover from elsewhere (the "Show" action of a started snackbar), and
/// the indicator's rows as of its last load, so a page can tell whether one of its records has a run going without a read
/// of its own. Scoped, so per circuit; the indicator listens and publishes while it is rendered.
/// </summary>
internal sealed class ActivityPanelState
{
    /// <summary>The user's runs as the indicator last read them: empty until its first load, and after a failed one.</summary>
    public IReadOnlyList<ActivityRow> Rows { get; private set; } = [];

    /// <summary>Raised after every load the indicator applies, on the circuit's synchronization context.</summary>
    public event Action? RowsChanged;

    public event Func<Task>? OpenRequested;

    /// <summary>Whether a run of <paramref name="commandName"/> on the one record <paramref name="targetId"/> is unfinished.</summary>
    public bool IsRunning(string commandName, Guid targetId) =>
        Rows.Any(row => row.IsRunning && row.TargetId == targetId && string.Equals(row.CommandName, commandName, StringComparison.Ordinal));

    /// <summary>The indicator's hand-over after a load: the rows, then <see cref="RowsChanged"/>.</summary>
    public void SetRows(IReadOnlyList<ActivityRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        Rows = rows;
        RowsChanged?.Invoke();
    }

    public Task OpenAsync() => OpenRequested?.Invoke() ?? Task.CompletedTask;

    /// <summary>The snackbar a background start answers with, its "Show" action opening the activity popover.</summary>
    public void NotifyStarted(ISnackbar snackbar, IStringLocalizer localizer, string message)
    {
        ArgumentNullException.ThrowIfNull(snackbar);
        ArgumentNullException.ThrowIfNull(localizer);

        snackbar.Add(message, Severity.Info, options =>
        {
            options.Action = localizer.Text(TaskUiTexts.Activity.Show);
            options.OnClick = _ => OpenAsync();
        });
    }
}
