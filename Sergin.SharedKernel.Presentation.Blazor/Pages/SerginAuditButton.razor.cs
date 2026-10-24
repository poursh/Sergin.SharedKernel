using System.Globalization;
using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Times;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// The detail page header's audit icon: a popover with who created and last changed the record and when, and the
/// row version the page holds — the one its edits and deletes are checked against.
/// </summary>
/// <remarks>
/// The page passes <see cref="Load"/>, normally a send of its aggregate's audit query, and the version it loaded.
/// Times are shown in the viewer's time zone and user ids as names from <see cref="IUserDirectory"/>, falling back
/// to a short id for a user the directory does not know.
/// </remarks>
public sealed partial class SerginAuditButton
{
    private AuditInfo? audit;
    private IReadOnlyDictionary<Guid, string> names = new Dictionary<Guid, string>();
    private TimeZoneInfo timeZone = TimeZoneInfo.Utc;
    private string? failure;
    private bool loading;

    [Parameter]
    [EditorRequired]
    public Func<Task<ErrorOr<AuditInfo>>> Load { get; set; } = default!;

    [Parameter]
    public RowVersion? Version { get; set; }

    [Inject]
    private IUserDirectory Users { get; set; } = default!;

    [Inject]
    private IUiTimeZoneStore TimeZones { get; set; } = default!;

    [Inject]
    private IUiErrorPresenter ErrorPresenter { get; set; } = default!;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    private string ShortVersion => Version is null ? string.Empty : Version.Value.ToString("N")[..12];

    private async Task OnOpenChangedAsync(bool open)
    {
        if (!open)
        {
            return;
        }

        loading = true;
        failure = null;

        ErrorOr<AuditInfo> loaded = await Load();

        if (loaded.IsError)
        {
            audit = null;
            failure = ErrorPresenter.Present(loaded.FirstError).Detail;
        }
        else
        {
            audit = loaded.Value;
            timeZone = await TimeZones.GetTimeZoneAsync();

            Guid[] ids = [.. new[] { audit.CreatedBy, audit.ModifiedBy ?? Guid.Empty }.Where(id => id != Guid.Empty).Distinct()];
            names = await Users.GetDisplayNamesAsync(ids);
        }

        loading = false;
    }

    private string FormatTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone)
            .ToString("dd MMM yyyy HH:mm", CultureInfo.CurrentCulture);

    private string NameOf(Guid? userId) =>
        userId is { } id
            ? names.GetValueOrDefault(id) ?? id.ToString("N")[..8]
            : string.Empty;
}
