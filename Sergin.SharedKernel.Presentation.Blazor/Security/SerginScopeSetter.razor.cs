using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Hosting;
using MudBlazor;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Application.Securities.Authorization;

namespace Sergin.SharedKernel.Presentation.Blazor.Security;

/// <summary>
/// A development tool for setting <see cref="IAuthorizationScopeContext"/> by hand, until the shell has a real
/// scope picker. It renders nothing outside Development.
/// </summary>
/// <remarks>
/// The value lives in the circuit, so a full reload starts again at the root. The dispatcher copies it into
/// every send, so the next request is checked and filtered on the new scope; <see cref="ScopeChanged"/> lets the
/// layout rebuild what it already rendered on the old one.
/// </remarks>
public sealed partial class SerginScopeSetter
{
    private static readonly DialogOptions DialogOptions = new()
    {
        MaxWidth = MaxWidth.Small,
        FullWidth = true,
        CloseOnEscapeKey = true,
    };

    private bool open;
    private bool applying;
    private string scopeText = string.Empty;
    private string? error;

    [Inject]
    private IHostEnvironment HostEnvironment { get; set; } = default!;

    [Inject]
    private IAuthorizationScopeContext ScopeContext { get; set; } = default!;

    [Inject]
    private IScopePathResolver PathResolver { get; set; } = default!;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    /// <summary>
    /// Raised after the scope changed.
    /// </summary>
    [Parameter]
    public EventCallback ScopeChanged { get; set; }

    private bool Visible => HostEnvironment.IsDevelopment();

    /// <summary>
    /// Composed here rather than through the localizer's arguments, which <c>DefaultLocalizer</c> ignores.
    /// </summary>
    private string Tooltip => $"{Localizer["Scope"]}: {(ScopeContext.IsRoot ? Localizer["root"].Value : ScopeContext.ScopeId.ToString())}";

    private void Open()
    {
        scopeText = ScopeContext.ScopeId.ToString();
        error = null;
        open = true;
    }

    private void Close() => open = false;

    /// <summary>
    /// Refuses a value that is not the id of a live scope, rather than letting every page fail closed on it.
    /// </summary>
    private async Task ApplyAsync()
    {
        if (!Guid.TryParse(scopeText.Trim(), out Guid scopeId) || scopeId == Guid.Empty)
        {
            error = Localizer["Enter a scope id."];
            return;
        }

        applying = true;

        try
        {
            if (await PathResolver.GetPathAsync(scopeId) is null)
            {
                error = Localizer["No scope has this id."];
                return;
            }
        }
        finally
        {
            applying = false;
        }

        ScopeContext.Set(scopeId);

        await ChangedAsync();
    }

    private async Task ResetAsync()
    {
        ScopeContext.Reset();

        await ChangedAsync();
    }

    private async Task ChangedAsync()
    {
        error = null;
        open = false;

        await ScopeChanged.InvokeAsync();
    }
}
