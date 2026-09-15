using Sergin.SharedKernel.Presentation.Errors;

namespace Sergin.SharedKernel.Presentation.Blazor.Errors;

public interface IUiErrorPresenter
{
    SerginProblem Present(Error error);

    void Notify(Error error);

    /// <summary>
    /// Notifies every error in the list — what a create or mutate submit should call with
    /// <c>result.Errors</c>, since validation yields one error per broken rule rather than a first one.
    /// </summary>
    void Notify(IReadOnlyList<Error> errors);
}
