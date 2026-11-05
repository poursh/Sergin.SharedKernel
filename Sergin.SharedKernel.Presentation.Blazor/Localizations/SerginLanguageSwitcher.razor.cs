using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Application.Localizations.Common;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Localizations;

/// <summary>
/// Lists the default language and every published one, plus the draft languages the caller may preview (marked as
/// drafts). Choosing one goes through <c>/culture/set</c>, a full navigation, because the culture belongs to the request
/// that opens the circuit.
/// </summary>
public sealed partial class SerginLanguageSwitcher
{
    private IReadOnlyList<LanguageChoice> languages = [];
    private string current = string.Empty;

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

    protected override async Task OnInitializedAsync()
    {
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

    private void OnChanged(ChangeEventArgs args)
    {
        string culture = args.Value?.ToString() ?? current;

        string returnUri = "/" + Navigation.ToBaseRelativePath(Navigation.Uri);

        Navigation.NavigateTo(
            $"/culture/set?culture={Uri.EscapeDataString(culture)}&redirectUri={Uri.EscapeDataString(returnUri)}",
            forceLoad: true);
    }

    private sealed record LanguageChoice(string Culture, string Label);
}
