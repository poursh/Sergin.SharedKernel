using Sergin.SharedKernel.Presentation.Errors;

namespace Sergin.SharedKernel.Presentation.Blazor.Errors;

public interface IUiErrorPresenter
{
    SerginProblem Present(Error error);

    void Notify(Error error);
}
