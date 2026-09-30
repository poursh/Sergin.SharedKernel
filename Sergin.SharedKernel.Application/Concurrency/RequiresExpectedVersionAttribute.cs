namespace Sergin.SharedKernel.Application.Concurrency;

/// <summary>
/// Marks a command that must carry an expected version in <see cref="ConcurrencyContext.Expected"/>. Without
/// one, ExpectedVersionPipelineBehavior answers <see cref="VersionErrors.Required"/> before the handler runs.
/// A version sent with an unmarked command is still checked: the attribute makes it required, not possible.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RequiresExpectedVersionAttribute : Attribute;
