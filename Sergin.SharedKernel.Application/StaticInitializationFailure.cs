namespace Sergin.SharedKernel.Application;

/// <summary>
/// Reads a <see cref="TypeInitializationException"/> the way a person debugging host start needs it. One static
/// class touching another whose initializer threw nests the exceptions ("the type initializer for 'Navigation'
/// threw", around "the type initializer for 'Permissions' threw", around the cause); the useful part is the
/// innermost class and its cause.
/// </summary>
internal static class StaticInitializationFailure
{
    /// <summary>
    /// An <see cref="InvalidOperationException"/> naming <paramref name="user"/> (what was being built), the class
    /// whose initializer failed first, and the message of the root cause. The original exception stays attached.
    /// </summary>
    public static InvalidOperationException Describe(string user, TypeInitializationException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        TypeInitializationException innermost = failure;

        while (innermost.InnerException is TypeInitializationException inner)
        {
            innermost = inner;
        }

        Exception cause = innermost.InnerException ?? innermost;

        return new InvalidOperationException(
            $"{user} failed: the static initializer of {innermost.TypeName} threw {cause.GetType().Name}: {cause.Message}",
            failure);
    }
}
