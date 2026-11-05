using System.Globalization;
using FluentValidation.Resources;

namespace Sergin.SharedKernel.Application.Localizations.Common;

/// <summary>
/// FluentValidation's message templates as declared texts, one per built-in validator, keyed
/// <c>common.validation.&lt;validator name in kebab case&gt;</c> (<c>NotEmptyValidator</c> is
/// <c>common.validation.not-empty-validator</c>). Each default is read from FluentValidation's own English resources,
/// so a host with no translations shows exactly what FluentValidation always did. The templates keep FluentValidation's
/// named placeholders (<c>{PropertyName}</c>, <c>{MaxLength}</c>, ...), which FluentValidation fills itself. Plus the
/// two repository rules of <c>RepositoryRuleBuilderExtensions</c>.
/// </summary>
public static class CommonValidationTexts
{
    private static readonly TextCategory category = TextCategory.Create("Common", "Validation");

    private static readonly LanguageManager english = new();

    private static readonly CultureInfo englishCulture = CultureInfo.GetCultureInfo("en");

    private static readonly Dictionary<string, TextDefinition> byValidator = new(StringComparer.Ordinal);

    public static TextDefinition AsyncPredicateValidator { get; } = Validator(nameof(AsyncPredicateValidator));

    public static TextDefinition CreditCardValidator { get; } = Validator(nameof(CreditCardValidator));

    public static TextDefinition EmailValidator { get; } = Validator(nameof(EmailValidator));

    public static TextDefinition EmptyValidator { get; } = Validator(nameof(EmptyValidator));

    public static TextDefinition EnumValidator { get; } = Validator(nameof(EnumValidator));

    public static TextDefinition EqualValidator { get; } = Validator(nameof(EqualValidator));

    public static TextDefinition ExactLengthValidator { get; } = Validator(nameof(ExactLengthValidator));

    public static TextDefinition ExclusiveBetweenValidator { get; } = Validator(nameof(ExclusiveBetweenValidator));

    public static TextDefinition GreaterThanOrEqualValidator { get; } = Validator(nameof(GreaterThanOrEqualValidator));

    public static TextDefinition GreaterThanValidator { get; } = Validator(nameof(GreaterThanValidator));

    public static TextDefinition InclusiveBetweenValidator { get; } = Validator(nameof(InclusiveBetweenValidator));

    public static TextDefinition LengthValidator { get; } = Validator(nameof(LengthValidator));

    public static TextDefinition LessThanOrEqualValidator { get; } = Validator(nameof(LessThanOrEqualValidator));

    public static TextDefinition LessThanValidator { get; } = Validator(nameof(LessThanValidator));

    public static TextDefinition MaximumLengthValidator { get; } = Validator(nameof(MaximumLengthValidator));

    public static TextDefinition MinimumLengthValidator { get; } = Validator(nameof(MinimumLengthValidator));

    public static TextDefinition NotEmptyValidator { get; } = Validator(nameof(NotEmptyValidator));

    public static TextDefinition NotEqualValidator { get; } = Validator(nameof(NotEqualValidator));

    public static TextDefinition NotNullValidator { get; } = Validator(nameof(NotNullValidator));

    public static TextDefinition NullValidator { get; } = Validator(nameof(NullValidator));

    public static TextDefinition PredicateValidator { get; } = Validator(nameof(PredicateValidator));

    public static TextDefinition RegularExpressionValidator { get; } = Validator(nameof(RegularExpressionValidator));

    public static TextDefinition ScalePrecisionValidator { get; } = Validator(nameof(ScalePrecisionValidator));

    // FluentValidation's short forms. Their resource names carry an underscore, which a key segment cannot.
    public static TextDefinition ExactLengthSimple { get; } = Validator("ExactLength_Simple", nameof(ExactLengthSimple));

    public static TextDefinition InclusiveBetweenSimple { get; } = Validator("InclusiveBetween_Simple", nameof(InclusiveBetweenSimple));

    public static TextDefinition LengthSimple { get; } = Validator("Length_Simple", nameof(LengthSimple));

    public static TextDefinition MaximumLengthSimple { get; } = Validator("MaximumLength_Simple", nameof(MaximumLengthSimple));

    public static TextDefinition MinimumLengthSimple { get; } = Validator("MinimumLength_Simple", nameof(MinimumLengthSimple));

    /// <summary><c>MustExistIn</c>: the value must be the id of an existing aggregate.</summary>
    public static TextDefinition MustExistIn { get; } =
        category.Text(nameof(MustExistIn), "'{PropertyName}' must refer to an existing {AggregateTypeName}.");

    /// <summary><c>MustBeUniqueIn</c>: the value must not already be carried by a row.</summary>
    public static TextDefinition MustBeUniqueIn { get; } =
        category.Text(nameof(MustBeUniqueIn), "'{PropertyName}' is already in use.");

    /// <summary>The declared template of FluentValidation's resource <paramref name="name"/>, or null for one not declared here.</summary>
    public static TextDefinition? ForValidator(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return byValidator.GetValueOrDefault(name);
    }

    private static TextDefinition Validator(string resourceName, string? keyName = null)
    {
        TextDefinition definition = category.Text(keyName ?? resourceName, english.GetString(resourceName, englishCulture));
        byValidator.Add(resourceName, definition);
        return definition;
    }
}
