namespace Sergin.SharedKernel.Presentation.Blazor.Validation;

public static class SerginFormValidatorExtensions
{
    /// <summary>
    /// Adapts <see cref="ISerginFormValidator.ValidateAsync{TRequest}"/> to the delegate shape
    /// <c>MudForm.Validation</c> takes, so a page writes
    /// <c>validation = FormValidator.RulesFor(ToCommand);</c> once and binds it to the form.
    /// </summary>
    /// <remarks>
    /// MudForm calls the delegate with <c>(Form.Model, For member path)</c>. The model argument is
    /// ignored: it is the page's form model, and the pipeline validator is written against the
    /// command, so <paramref name="request"/> builds the command from the form model on each call —
    /// the same <c>ToCommand()</c> the page submits, so the two can't drift.
    /// </remarks>
    public static Func<object, string, Task<IEnumerable<string>>> RulesFor<TRequest>(
        this ISerginFormValidator validator, Func<TRequest> request)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(request);

        return async (_, propertyName) => await validator.ValidateAsync(request(), propertyName);
    }
}
