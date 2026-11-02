namespace Sergin.SharedKernel.Presentation.Blazor.Trees;

/// <summary>How a viewer left a tree page's panel: its width, whether it was hidden, the group that was active.</summary>
/// <param name="Width">The panel width in pixels; null for the page's default.</param>
/// <param name="Hidden">Whether the panel was folded to its rail.</param>
/// <param name="Group">The key of the last active group; null for the first.</param>
public sealed record SerginTreeLayout(int? Width, bool Hidden, string? Group)
{
    public static SerginTreeLayout Default { get; } = new(null, Hidden: false, Group: null);
}

/// <summary>
/// Remembers a tree page's <see cref="SerginTreeLayout"/> per page key, so a reload returns to it.
/// </summary>
/// <remarks>
/// <b>Best-effort</b>, like <see cref="Theming.IUiThemeStore"/>: a browser that refuses storage reads as
/// <see cref="SerginTreeLayout.Default"/>, and no method throws for a storage failure. Backed by JS interop, so every
/// method must be called <b>after</b> the first render.
/// </remarks>
public interface IUiTreeLayoutStore
{
    ValueTask<SerginTreeLayout> LoadAsync(string pageKey, CancellationToken cancellationToken = default);

    ValueTask SaveAsync(string pageKey, SerginTreeLayout layout, CancellationToken cancellationToken = default);
}
