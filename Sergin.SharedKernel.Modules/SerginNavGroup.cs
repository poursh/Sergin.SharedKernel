using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Modules;

/// <summary>
/// The drawer group a UI module's entries are shown under, and the module step of a breadcrumb trail.
/// </summary>
/// <param name="Title">Display text, usually the module's name: a declared text rendered in the current UI culture.</param>
/// <param name="Icon">Raw SVG path data. A string keeps this contract leaf free of any UI library.</param>
/// <param name="Order">Ordering among the groups and the home entry; ties broken by Title.</param>
public sealed record SerginNavGroup(TextDefinition Title, string Icon, int Order = 0)
{
    /// <summary>
    /// Transitional: a title written as a plain string, shown as written (<see cref="TransitionalText"/>). Goes once
    /// every module declares its nav texts.
    /// </summary>
    public SerginNavGroup(string Title, string Icon, int Order = 0)
        : this(TransitionalText.Of(Title), Icon, Order)
    {
    }
}
