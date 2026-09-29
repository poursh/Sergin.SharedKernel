namespace Sergin.SharedKernel.Application.Concurrency;

/// <summary>
/// A save found the aggregate's row at another version than the one expected. Thrown by SerginDbContext in
/// place of EF's DbUpdateConcurrencyException, so the Application layer can catch it without referencing EF.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
    {
    }

    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
