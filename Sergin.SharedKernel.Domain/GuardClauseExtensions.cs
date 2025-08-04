using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Ardalis.GuardClauses;

public static class GuardClauseExtensions
{
    public static string InvalidStringGuid(this IGuardClause guardClause,
        [NotNull][ValidatedNotNull] string? input,        
        string? message = null,
        Func<Exception>? exceptionCreator = null,
        [CallerArgumentExpression(nameof(input))] string? parameterName = null)
    {
        guardClause.NullOrEmpty(input, parameterName, message, exceptionCreator);

        if (!Guid.TryParse(input, out _))
        {
            throw exceptionCreator?.Invoke() ??
                new ArgumentException(message ?? $"The input {parameterName} was not a valid Guid.", parameterName);
        }

        return input;
    }
}
