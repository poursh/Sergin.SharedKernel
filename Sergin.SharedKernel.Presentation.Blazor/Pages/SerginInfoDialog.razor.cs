using System.Globalization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Security;
using Sergin.SharedKernel.Presentation.Blazor.Times;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// The modal <see cref="SerginInfoButton"/> opens: the scope the record belongs to, the row version the page holds,
/// and who created and last changed the record and when.
/// </summary>
/// <remarks>
/// The scope is named from the scopes <see cref="SerginScopeSession"/> already lists for the circuit — the record is
/// readable only inside the caller's reach, so its scope is normally among them — with its listed ancestors as the
/// path; a scope not listed falls back to a short id. Times are shown in the viewer's time zone and user ids as names
/// from <see cref="IUserDirectory"/>, <see cref="SystemActor"/> as "System", falling back to a short id for a user the
/// directory does not know.
/// </remarks>
public sealed partial class SerginInfoDialog
{
    private const string PathSeparator = " › ";

    private AuditInfo? audit;
    private IReadOnlyDictionary<Guid, string> names = new Dictionary<Guid, string>();
    private TimeZoneInfo timeZone = TimeZoneInfo.Utc;
    private string? failure;
    private bool loading = true;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public Func<Task<ErrorOr<AuditInfo>>> Load { get; set; } = default!;

    [Parameter]
    public RowVersion? Version { get; set; }

    [Inject]
    private SerginScopeSession Scopes { get; set; } = default!;

    [Inject]
    private IUserDirectory Users { get; set; } = default!;

    [Inject]
    private IUiTimeZoneStore TimeZones { get; set; } = default!;

    [Inject]
    private IUiErrorPresenter ErrorPresenter { get; set; } = default!;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    private string ShortVersion => Version is null ? string.Empty : Version.Value.ToString("N")[..12];

    private ScopeOption? Scope =>
        audit?.ScopeId is { } scopeId ? Scopes.Options.FirstOrDefault(option => option.Id == scopeId) : null;

    private string ScopeName =>
        Scope?.Name ?? audit?.ScopeId?.ToString("N")[..8] ?? string.Empty;

    // The path only when the scope has a listed ancestor; a top-level scope's path would repeat its name.
    private string? ScopePath =>
        Scope is { } scope && Scopes.TrailOf(scope) is { Count: > 1 } trail
            ? string.Join(PathSeparator, trail.Select(option => option.Name))
            : null;

    protected override async Task OnInitializedAsync()
    {
        ErrorOr<AuditInfo> loaded = await Load();

        if (loaded.IsError)
        {
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

    // The system actor has no user row, so the directory never names it; any other unknown id shows short.
    private string NameOf(Guid? userId) =>
        userId switch
        {
            null => string.Empty,
            { } id when id == SystemActor.Id => Localizer[SystemActor.DisplayName],
            { } id => names.GetValueOrDefault(id) ?? id.ToString("N")[..8],
        };

    private void Close() => MudDialog.Close();
}
