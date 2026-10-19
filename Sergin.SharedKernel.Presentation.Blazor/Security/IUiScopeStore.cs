namespace Sergin.SharedKernel.Presentation.Blazor.Security;

/// <summary>
/// Remembers the scope a user last picked, so a reload returns to it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Best-effort</b>, like <see cref="Theming.IUiThemeStore"/>: a browser that refuses storage degrades to the
/// user's home scope, and no method throws for a storage failure.
/// </para>
/// <para>
/// Backed by JS interop, so every method must be called <b>after</b> the first render.
/// </para>
/// <para>
/// Keyed by user: a scope stored by another user of the same browser reads as nothing stored. A stored scope is a
/// preference, not a grant — the caller still checks it against what the user can reach.
/// </para>
/// </remarks>
public interface IUiScopeStore
{
    /// <summary>The scope <paramref name="userId"/> last picked in this browser, or <see langword="null"/>.</summary>
    ValueTask<Guid?> LoadAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Records <paramref name="scopeId"/> as the scope <paramref name="userId"/> picked.</summary>
    ValueTask SaveAsync(Guid userId, Guid scopeId, CancellationToken cancellationToken = default);
}
