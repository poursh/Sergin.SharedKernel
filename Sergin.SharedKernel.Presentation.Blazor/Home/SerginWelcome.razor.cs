using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Home;

/// <summary>
/// The default home slot: names the application and points at the nav menu. It exists so that the site
/// root always renders something, rather than a host having to supply a landing page before the app runs.
/// </summary>
public sealed partial class SerginWelcome
{
    [Inject]
    private IOptions<SerginApplicationOptions> ApplicationOptions { get; set; } = default!;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    private string ApplicationName => ApplicationOptions.Value.ApplicationName;

    /// <summary>
    /// Single-argument lookup on purpose. <c>ILocalizer</c>'s <c>params object[]</c> overload discards its
    /// arguments, so a composed key such as <c>"Welcome to {0}"</c> would render the placeholder verbatim.
    /// </summary>
    private string Prompt => Localizer["Choose a section from the menu to get started."];
}
