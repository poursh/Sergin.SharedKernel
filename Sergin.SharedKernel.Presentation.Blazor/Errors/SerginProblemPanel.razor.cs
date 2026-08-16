using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using MudBlazor;
using Sergin.SharedKernel.Presentation.Errors;

namespace Sergin.SharedKernel.Presentation.Blazor.Errors;

public sealed partial class SerginProblemPanel
{
    [Parameter]
    public SerginProblem? Problem { get; set; }

    private Severity AlertSeverity
        => Problem?.StatusCode switch
        {
            StatusCodes.Status404NotFound => Severity.Info,
            StatusCodes.Status403Forbidden or StatusCodes.Status409Conflict => Severity.Warning,
            _ => Severity.Error
        };
}
