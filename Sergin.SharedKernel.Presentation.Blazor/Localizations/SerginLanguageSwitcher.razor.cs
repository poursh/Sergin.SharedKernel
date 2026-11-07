using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.Localizations.Common;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Localizations;

/// <summary>
/// Lists the default language and every published one, plus the draft languages the caller may preview (marked as
/// drafts), in a menu. Each item navigates to <c>/culture/set</c> with the current page as <c>redirectUri</c>, a full
/// navigation, because the culture belongs to the request that opens the circuit. The return address follows in-app
/// navigation.
/// </summary>
public sealed partial class SerginLanguageSwitcher : IDisposable
{
    /// <summary>The switch endpoint (the WebUi host's <c>CultureEndpoints.SetPath</c>).</summary>
    private const string SetPath = "/culture/set";

    private IReadOnlyList<LanguageChoice> languages = [];
    private string current = string.Empty;
    private string returnUri = "/";

    [Inject]
    private ITranslationCache Cache { get; set; } = default!;

    [Inject]
    private IDraftLanguagePreviewPolicy PreviewPolicy { get; set; } = default!;

    [Inject]
    private IOptions<SerginLocalizationOptions> Options { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private string Label => Localizer.Text(CommonTexts.Language);

    private string CurrentLabel => languages.FirstOrDefault(IsCurrent)?.Label ?? current;

    public void Dispose() => Navigation.LocationChanged -= OnLocationChanged;

    protected override async Task OnInitializedAsync()
    {
        returnUri = CurrentRelativeUri();
        Navigation.LocationChanged += OnLocationChanged;

        string defaultCulture = Options.Value.DefaultCulture;
        bool canPreview = Cache.Languages.Any(language => !language.IsPublished)
            && await PreviewPolicy.CanPreviewAsync(CancellationToken.None);

        languages = LanguageChoices.Build(defaultCulture, Cache.Languages, canPreview, Localizer.Text(CommonTexts.DraftSuffix));
        current = languages.FirstOrDefault(choice => string.Equals(choice.Culture, CultureInfo.CurrentUICulture.Name, StringComparison.OrdinalIgnoreCase))?.Culture
            ?? defaultCulture;
    }

    private bool IsCurrent(LanguageChoice language) => string.Equals(language.Culture, current, StringComparison.OrdinalIgnoreCase);

    private string HrefFor(LanguageChoice language) =>
        $"{SetPath}?culture={Uri.EscapeDataString(language.Culture)}&redirectUri={Uri.EscapeDataString(returnUri)}";

    private string CurrentRelativeUri() => "/" + Navigation.ToBaseRelativePath(Navigation.Uri);

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        returnUri = CurrentRelativeUri();
        _ = InvokeAsync(StateHasChanged);
    }
}
