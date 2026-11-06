using FluentValidation;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Application.Localizations.Common;
using Sergin.SharedKernel.Application.Localizations.Validation;

namespace Sergin.SharedKernel.Application.Validations;

/// <summary>
/// FluentValidation rules backed by a repository: a reference must point at an existing aggregate, a value object
/// declared as an alternate key must not be in use. Both run inside <c>ValidationPipelineBehavior</c>, in the
/// request's scope, before the handler; both are advisory — the foreign key / unique index behind them is the
/// guarantee. Guard them with <c>.When(...)</c> so no query runs for a value whose shape rule already failed. Their
/// messages are the declared texts <see cref="CommonValidationTexts.MustExistIn"/> (naming the target by a declared
/// text the caller passes) and <see cref="CommonValidationTexts.MustBeUniqueIn"/>, in the caller's culture.
/// </summary>
public static class RepositoryRuleBuilderExtensions
{
    /// <summary>The placeholder <see cref="CommonValidationTexts.MustExistIn"/> names the aggregate with.</summary>
    public const string AggregateTypeNamePlaceholder = "AggregateTypeName";

    /// <summary>
    /// The value must be the id of an existing <typeparamref name="TAggregateRoot"/> — a
    /// <see cref="IRepository{TAggregateRoot, TId}.ExistsAsync"/>, so nothing is loaded — named in the message by
    /// <paramref name="aggregateName"/>, a declared text (usually the root's <c>Singular</c>) in the caller's culture.
    /// There is deliberately no overload without one: a type name would be English inside a translated sentence.
    /// </summary>
    public static IRuleBuilderOptions<T, TId> MustExistIn<T, TAggregateRoot, TId>(
        this IRuleBuilder<T, TId> ruleBuilder,
        IRepository<TAggregateRoot, TId> repository,
        TextDefinition aggregateName)
        where TAggregateRoot : class, IAggregateRoot<TId>
        where TId : notnull
    {
        ArgumentNullException.ThrowIfNull(repository);

        return ruleBuilder.MustExistIn(repository.ExistsAsync, aggregateName);
    }

    /// <summary>
    /// The value must refer to something <paramref name="exists"/> finds — for a reference with no repository of its
    /// own, such as an entity inside another aggregate, or an id the command carries untyped. The message is
    /// <see cref="CommonValidationTexts.MustExistIn"/>, naming the target by <paramref name="aggregateName"/> in the
    /// caller's culture.
    /// </summary>
    public static IRuleBuilderOptions<T, TValue> MustExistIn<T, TValue>(
        this IRuleBuilder<T, TValue> ruleBuilder,
        Func<TValue, CancellationToken, Task<bool>> exists,
        TextDefinition aggregateName)
    {
        ArgumentNullException.ThrowIfNull(ruleBuilder);
        ArgumentNullException.ThrowIfNull(exists);
        ArgumentNullException.ThrowIfNull(aggregateName);

        return ruleBuilder
            .MustAsync((_, value, context, cancellationToken) =>
            {
                context.MessageFormatter.AppendArgument(AggregateTypeNamePlaceholder, ValidationTexts.Template(aggregateName));
                return exists(value, cancellationToken);
            })
            .WithMessage(_ => ValidationTexts.Template(CommonValidationTexts.MustExistIn));
    }

    /// <summary>
    /// The value must not already be carried by any row — a
    /// <see cref="IUniqueKeyRepository{TKey}.IsTakenAsync"/> answering <see langword="false"/>.
    /// </summary>
    public static IRuleBuilderOptions<T, TKey> MustBeUniqueIn<T, TKey>(
        this IRuleBuilder<T, TKey> ruleBuilder,
        IUniqueKeyRepository<TKey> repository)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(ruleBuilder);
        ArgumentNullException.ThrowIfNull(repository);

        return ruleBuilder
            .MustAsync(async (key, cancellationToken) => !await repository.IsTakenAsync(key, cancellationToken))
            .WithMessage(_ => ValidationTexts.Template(CommonValidationTexts.MustBeUniqueIn));
    }
}
