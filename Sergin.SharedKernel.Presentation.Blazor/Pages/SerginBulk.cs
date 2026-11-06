using MudBlazor;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>What a bulk run did: how many items succeeded, the errors of the rest, and how many were never sent.</summary>
public sealed record SerginBulkOutcome(int Succeeded, IReadOnlyList<Error> Errors, int Skipped)
{
    /// <summary>
    /// Reports the run: one success snackbar naming the count (<paramref name="doneText"/>, a declared text with a
    /// <c>{Count}</c> placeholder such as <see cref="CommonTexts.BulkDeleted"/>), each distinct error once — ten
    /// identical "in use" refusals show as one — and the items a stop left unsent
    /// (<see cref="CommonTexts.BulkNotSent"/>). Counts go through <paramref name="formatter"/>, so a Persian page shows
    /// Persian digits.
    /// </summary>
    public void Report(
        IUiErrorPresenter errorPresenter,
        ISnackbar snackbar,
        IStringLocalizer localizer,
        ICultureFormatter formatter,
        TextDefinition doneText)
    {
        ArgumentNullException.ThrowIfNull(errorPresenter);
        ArgumentNullException.ThrowIfNull(snackbar);
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(formatter);
        ArgumentNullException.ThrowIfNull(doneText);

        if (Succeeded > 0)
        {
            snackbar.Add(localizer.Text(doneText, new { Count = formatter.Number(Succeeded) }), Severity.Success);
        }

        if (Errors.Count > 0)
        {
            errorPresenter.Notify([.. Errors.Distinct()]);
        }

        if (Skipped > 0)
        {
            snackbar.Add(localizer.Text(CommonTexts.BulkNotSent, new { Count = formatter.Number(Skipped) }), Severity.Info);
        }
    }
}

/// <summary>The loop behind every bulk action: one command per selected item, one after another.</summary>
/// <remarks>
/// There is no bulk command on the server. Each item goes through the pipeline as the row command would send it —
/// permission, version and validation checked per item — so one refusal does not undo the others.
/// </remarks>
public static class SerginBulk
{
    /// <summary>
    /// Sends <paramref name="send"/> for each item in order. An error for which <paramref name="stopWhen"/> answers
    /// true ends the run, and the items after it count as skipped — e.g. a stale version, which every later send
    /// at the same version would also hit.
    /// </summary>
    public static async Task<SerginBulkOutcome> RunAsync<TItem>(
        IReadOnlyList<TItem> items,
        Func<TItem, Task<ErrorOr<Success>>> send,
        Func<Error, bool>? stopWhen = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(send);

        int succeeded = 0;
        List<Error> errors = [];

        for (int index = 0; index < items.Count; index++)
        {
            ErrorOr<Success> result = await send(items[index]);

            if (!result.IsError)
            {
                succeeded++;
                continue;
            }

            errors.AddRange(result.Errors);

            if (stopWhen is not null && result.Errors.Exists(error => stopWhen(error)))
            {
                return new SerginBulkOutcome(succeeded, errors, items.Count - index - 1);
            }
        }

        return new SerginBulkOutcome(succeeded, errors, 0);
    }
}
