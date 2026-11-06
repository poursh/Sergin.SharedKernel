using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Application.Localizations.Common;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Localizations;

/// <summary>
/// Lists the default language and every published one, plus the draft languages the caller may preview (marked as
/// drafts). Choosing one submits a GET form to <c>/culture/set</c> carrying the current page as <c>redirectUri</c>, a full
/// navigation, because the culture belongs to the request that opens the circuit; being a plain form, it works in the
/// prerendered page before the circuit connects. The return address follows in-app navigation once it has.
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

    public void Dispose() => Navigation.LocationChanged -= OnLocationChanged;

    protected override async Task OnInitializedAsync()
    {
        returnUri = CurrentRelativeUri();
        Navigation.LocationChanged += OnLocationChanged;

        string defaultCulture = Options.Value.DefaultCulture;
        bool canPreview = Cache.Languages.Any(language => !language.IsPublished)
            && await PreviewPolicy.CanPreviewAsync(CancellationToken.None);

        List<LanguageChoice> choices =
        [
            new(defaultCulture, CultureInfo.GetCultureInfo(defaultCulture).NativeName),
        ];

        foreach (LanguageInfo language in Cache.Languages)
        {
            if (string.Equals(language.Culture, defaultCulture, StringComparison.OrdinalIgnoreCase)
                || !language.IsPublished && !canPreview)
            {
                continue;
            }

            string name = language.IsPublished
                ? language.DisplayName
                : $"{language.DisplayName} ({Localizer.Text(CommonTexts.DraftSuffix)})";

            choices.Add(new LanguageChoice(language.Culture, name));
        }

        languages = choices;
        current = choices.FirstOrDefault(choice => string.Equals(choice.Culture, CultureInfo.CurrentUICulture.Name, StringComparison.OrdinalIgnoreCase))?.Culture
            ?? defaultCulture;
    }

    private string CurrentRelativeUri() => "/" + Navigation.ToBaseRelativePath(Navigation.Uri);

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        returnUri = CurrentRelativeUri();
        _ = InvokeAsync(StateHasChanged);
    }

    private sealed record LanguageChoice(string Culture, string Label);
}
