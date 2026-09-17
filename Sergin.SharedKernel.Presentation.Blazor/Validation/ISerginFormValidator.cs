namespace Sergin.SharedKernel.Presentation.Blazor.Validation;

/// <summary>
/// Runs the pipeline's own FluentValidation validator for one property of a request, so a
/// Blazor form can show the same rule, with the same message, while the user types — a taken
/// user name, a device model that does not exist — instead of only after submit.
/// </summary>
/// <remarks>
/// Blazor-only, like <see cref="Dispatching.ISerginDispatcher"/>: a WebApi endpoint has no field
/// to validate ahead of the pipeline. The pipeline's ValidationPipelineBehavior still runs every
/// rule on submit, so this is a preview, not a replacement — a rule on a property the form has no
/// field for, or a race on a unique key, is still refused there.
/// </remarks>
public interface ISerginFormValidator
{
    /// <summary>
    /// Runs only the rules the pipeline's <c>IValidator&lt;TRequest&gt;</c> declares for
    /// <paramref name="propertyName"/> (FluentValidation's <c>IncludeProperties</c>), in a fresh DI
    /// scope, and returns their messages. A request type with no validator registered yields nothing.
    /// </summary>
    /// <remarks>
    /// <paramref name="propertyName"/> is matched against each rule's effective property name — the
    /// one <c>OverridePropertyName</c> sets on a <c>.Value</c> rule, or the member name of a rule on
    /// the wrapper itself. A validator that forgets <c>OverridePropertyName</c> on a <c>.Value</c>
    /// rule is silently skipped here and still refuses the command on submit.
    /// </remarks>
    Task<IReadOnlyCollection<string>> ValidateAsync<TRequest>(
        TRequest request, string propertyName, CancellationToken cancellationToken = default);
}
