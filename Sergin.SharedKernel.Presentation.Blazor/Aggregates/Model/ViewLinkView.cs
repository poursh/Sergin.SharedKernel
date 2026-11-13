namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>A link to another page a configuration declared with <c>Link(...)</c>: an action on the detail bar and an icon on a list row.</summary>
/// <param name="Label">The link's text: the button's, and the icon's tooltip and accessible name.</param>
/// <param name="Icon">A MudBlazor icon constant.</param>
/// <param name="Href">Builds the page's route from the record's id.</param>
public sealed record ViewLinkView(TextDefinition Label, string Icon, Func<Guid, string> Href);
