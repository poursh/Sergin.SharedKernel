using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Infrastructure.Localizations;

/// <summary>
/// The preview policy of a host without a translation module: nobody previews a draft language. SharedKernel defines
/// no permission for it; the module owning translations replaces this with a permission check.
/// </summary>
internal sealed class DenyDraftPreview : IDraftLanguagePreviewPolicy
{
    public Task<bool> CanPreviewAsync(CancellationToken cancellationToken) => Task.FromResult(false);
}
