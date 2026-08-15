using ErrorOr;

namespace Sergin.SharedKernel.Presentation.Errors;

public sealed record SerginProblem(int StatusCode, string Title, string Detail, ErrorType Type);
