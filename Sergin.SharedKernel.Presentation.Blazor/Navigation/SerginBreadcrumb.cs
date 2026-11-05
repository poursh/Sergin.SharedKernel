using Sergin.SharedKernel.Modules;

namespace Sergin.SharedKernel.Presentation.Blazor.Navigation;

/// <summary>
/// One step of a page's breadcrumb trail, declared by the page and rendered by <see cref="SerginBreadcrumbs"/>: a
/// declared text (<c>New device</c>) or, through <see cref="Data"/>, a value the user owns (a device id), which is
/// shown as is and never looked up.
/// </summary>
public sealed record SerginBreadcrumb
{
    /// <summary>A step labelled with a declared text, rendered in the current UI culture.</summary>
    /// <param name="label">The step's text.</param>
    /// <param name="href">
    /// Absolute, schema-prefixed path, or <see langword="null"/> for a step that is not a link. The last step of a
    /// trail is the current page and is never a link, whatever this says; <see cref="SerginBreadcrumbs"/> enforces
    /// that so no page has to.
    /// </param>
    public SerginBreadcrumb(TextDefinition label, string? href = null)
        : this(label ?? throw new ArgumentNullException(nameof(label)), value: null, href)
    {
    }

    /// <summary>
    /// Transitional: a step written as a plain string, shown as written — what <see cref="Data"/> does. Goes once every
    /// module declares its texts.
    /// </summary>
    public SerginBreadcrumb(string label, string? href = null)
        : this(text: null, label ?? throw new ArgumentNullException(nameof(label)), href)
    {
    }

    private SerginBreadcrumb(TextDefinition? text, string? value, string? href)
    {
        Label = text;
        Value = value;
        Href = href;
    }

    /// <summary>The declared text the step shows; null for a <see cref="Data"/> step.</summary>
    public TextDefinition? Label { get; }

    /// <summary>The value a <see cref="Data"/> step shows as is; null for a declared one.</summary>
    public string? Value { get; }

    public string? Href { get; }

    /// <summary>
    /// The step for a module section, taken from the same nav entry the drawer renders so the two cannot drift.
    /// </summary>
    public static SerginBreadcrumb Of(SerginNavItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new(item.Label, item.Href);
    }

    /// <summary>A step showing <paramref name="value"/> — user data such as a device id or a scope name — unlocalised.</summary>
    public static SerginBreadcrumb Data(string value, string? href = null)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new(text: null, value, href);
    }

    /// <summary>The text the step shows in the current UI culture.</summary>
    public string Display(IStringLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        return Label is null ? Value ?? string.Empty : localizer.Text(Label);
    }
}
