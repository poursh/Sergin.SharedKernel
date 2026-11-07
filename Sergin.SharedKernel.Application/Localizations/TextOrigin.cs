namespace Sergin.SharedKernel.Application.Localizations;

/// <summary>
/// Where a declared text comes from, so a translator can tell screens from messages. It is a tag on the catalog entry,
/// never part of the key: a text that moves between an Application and a UI class keeps its key and its translations,
/// and only its origin changes.
/// </summary>
public enum TextOrigin
{
    /// <summary>Declared beside the application: field labels, validation and error texts, permission labels.</summary>
    Application,

    /// <summary>Declared beside the pages: titles, actions, confirms, the shell and MudBlazor's own strings.</summary>
    Ui,
}
