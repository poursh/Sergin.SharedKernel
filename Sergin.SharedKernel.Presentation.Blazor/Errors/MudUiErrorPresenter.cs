using Microsoft.AspNetCore.Http;
using MudBlazor;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Presentation.Errors;

namespace Sergin.SharedKernel.Presentation.Blazor.Errors;

internal sealed class MudUiErrorPresenter(ILocalizer localizer, ISnackbar snackbar) : IUiErrorPresenter
{
    public SerginProblem Present(Error error) => SerginProblemFactory.Create(error, localizer);

    public void Notify(Error error)
    {
        SerginProblem problem = Present(error);

        snackbar.Add(problem.Detail, ToSeverity(problem.StatusCode));
    }

    private static Severity ToSeverity(int statusCode)
        => statusCode switch
        {
            StatusCodes.Status404NotFound => Severity.Info,
            StatusCodes.Status403Forbidden or StatusCodes.Status409Conflict => Severity.Warning,
            _ => Severity.Error
        };
}
