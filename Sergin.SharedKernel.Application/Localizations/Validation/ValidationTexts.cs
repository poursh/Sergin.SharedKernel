using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using FluentValidation;
using FluentValidation.Resources;

namespace Sergin.SharedKernel.Application.Localizations.Validation;

/// <summary>
/// Connects FluentValidation's process-wide hooks to one host's texts for the duration of one validation.
/// <para>
/// <c>ValidatorOptions.Global</c> is static, and a test process builds many hosts, each with its own catalog,
/// translation store and cache; pointing the globals at one host's services would hand every other host that host's
/// texts, or a disposed provider. So <see cref="Install"/> sets the globals once, idempotently, to stateless
/// forwarders, and each validation runs inside <see cref="Use"/>, which makes this host's
/// <see cref="TextLanguageManager"/> and <see cref="FieldDisplayNameResolver"/> current for its own async flow only
/// (an <see cref="AsyncLocal{T}"/>). FluentValidation builds messages and display names while the validator runs, so
/// both are read inside that flow, under the caller's UI culture. Outside one — a validator run by hand — the
/// forwarders answer what FluentValidation's own defaults would.
/// </para>
/// </summary>
public sealed class ValidationTexts(TextLanguageManager languageManager, FieldDisplayNameResolver displayNames)
{
    private static readonly AsyncLocal<ValidationTexts?> current = new();
    private static readonly Lock installLock = new();

    public TextLanguageManager LanguageManager { get; } = languageManager;

    public FieldDisplayNameResolver DisplayNames { get; } = displayNames;

    /// <summary>The texts of the validation running in this async flow, or null.</summary>
    public static ValidationTexts? Current => current.Value;

    /// <summary>
    /// Points <c>ValidatorOptions.Global.LanguageManager</c> and <c>DisplayNameResolver</c> at the forwarders. Safe to
    /// call from every host start: the second and later calls change nothing.
    /// </summary>
    public static void Install()
    {
        lock (installLock)
        {
            if (ValidatorOptions.Global.LanguageManager is AmbientLanguageManager)
            {
                return;
            }

            ILanguageManager fallbackLanguage = ValidatorOptions.Global.LanguageManager;
            Func<Type, MemberInfo, LambdaExpression, string> fallbackDisplayName = ValidatorOptions.Global.DisplayNameResolver;

            ValidatorOptions.Global.LanguageManager = new AmbientLanguageManager(fallbackLanguage);
            ValidatorOptions.Global.DisplayNameResolver = (type, member, expression) =>
                current.Value is { } texts
                    ? texts.DisplayNames.Resolve(type, member, expression) ?? fallbackDisplayName(type, member, expression)
                    : fallbackDisplayName(type, member, expression);
        }
    }

    /// <summary>
    /// <paramref name="definition"/>'s message template in the current validation's culture and host, or its default
    /// outside a validation. For a rule's own <c>WithMessage</c>: FluentValidation fills the placeholders.
    /// </summary>
    public static string Template(TextDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return current.Value is { } texts ? texts.LanguageManager.Template(definition) : definition.Default;
    }

    /// <summary>Makes these texts current for this async flow until the result is disposed.</summary>
    public IDisposable Use()
    {
        ValidationTexts? previous = current.Value;
        current.Value = this;
        return new Restore(previous);
    }

    private sealed class Restore(ValidationTexts? previous) : IDisposable
    {
        public void Dispose() => current.Value = previous;
    }

    /// <summary>The current validation's <see cref="TextLanguageManager"/>; FluentValidation's previous manager outside one.</summary>
    private sealed class AmbientLanguageManager(ILanguageManager fallback) : ILanguageManager
    {
        public bool Enabled
        {
            get => Target.Enabled;
            set => Target.Enabled = value;
        }

        public CultureInfo Culture
        {
            get => Target.Culture;
            set => Target.Culture = value;
        }

        private ILanguageManager Target => (ILanguageManager?)current.Value?.LanguageManager ?? fallback;

        public string GetString(string key, CultureInfo? culture = null) => Target.GetString(key, culture);
    }
}
