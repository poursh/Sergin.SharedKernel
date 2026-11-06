using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

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
    private IStringLocalizer Localizer { get; set; } = default!;

    private string ApplicationName => ApplicationOptions.Value.ApplicationName;

    private string Prompt => Localizer.Text(ShellTexts.Welcome.Prompt);
}
