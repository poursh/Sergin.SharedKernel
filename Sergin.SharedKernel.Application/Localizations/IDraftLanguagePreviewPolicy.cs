namespace Sergin.SharedKernel.Application.Localizations;

/// <summary>
/// Whether the current caller may switch to a language that is still a draft, to check its translations in context.
/// Asked by the culture provider only when the culture cookie names a draft language; everyone it refuses gets the
/// default culture. SharedKernel's own answer is always no; the module owning translations supplies the real one.
/// </summary>
public interface IDraftLanguagePreviewPolicy
{
    Task<bool> CanPreviewAsync(CancellationToken cancellationToken);
}
