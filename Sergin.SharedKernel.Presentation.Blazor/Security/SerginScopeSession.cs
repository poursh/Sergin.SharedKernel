using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Application.Securities.Users;

namespace Sergin.SharedKernel.Presentation.Blazor.Security;

/// <summary>
/// The circuit's side of <see cref="IAuthorizationScopeContext"/>: the scopes the user can pick, which one is
/// current, and the rules for choosing it — the home scope at start, the stored pick after the first render, the
/// picker's choice after that.
/// </summary>
/// <remarks>
/// Registered scoped, so in Blazor Server one per circuit (and a separate one for the prerender). The layout calls
/// <see cref="InitializeAsync"/> before it renders the page, so a page never loads on a scope the user cannot work in.
/// </remarks>
internal sealed class SerginScopeSession(
    IScopeDirectory directory,
    IAuthorizationScopeContext scopeContext,
    IUiScopeStore store,
    IUserContext userContext)
{
    /// <summary>The listed scopes in tree order: each scope followed by its listed children, siblings by name.</summary>
    public IReadOnlyList<ScopeOption> Options { get; private set; } = [];

    /// <summary>The listed scope the circuit works in; null when the user can work in none.</summary>
    public ScopeOption? Current => Find(scopeContext.ScopeId);

    /// <summary>
    /// Loads the listed scopes and moves to the home scope when the current one is not listed. No storage is read,
    /// so it runs while prerendering.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshAsync(cancellationToken);
    }

    /// <summary>
    /// Reloads the listed scopes, so a grant change shows the next time the picker opens. Returns whether the
    /// current scope changed because it dropped out of the list.
    /// </summary>
    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        Options = InTreeOrder(await directory.GetReachableAsync(cancellationToken));

        if (Current is not null || ScopeOption.HomeOf(Options) is not { } home)
        {
            return false;
        }

        scopeContext.Set(home.Id);

        return true;
    }

    /// <summary>
    /// Returns to the scope stored for this user, when it is still listed. Interop: call after the first render.
    /// Returns whether the current scope changed.
    /// </summary>
    public async Task<bool> RestoreAsync(CancellationToken cancellationToken = default)
    {
        Guid? stored = await store.LoadAsync(userContext.Id.Value, cancellationToken);

        return stored is { } scopeId && Move(scopeId);
    }

    /// <summary>Moves to a listed scope and stores the choice. Returns whether the current scope changed.</summary>
    public async Task<bool> SelectAsync(Guid scopeId, CancellationToken cancellationToken = default)
    {
        if (!Move(scopeId))
        {
            return false;
        }

        await store.SaveAsync(userContext.Id.Value, scopeId, cancellationToken);

        return true;
    }

    /// <summary>The listed ancestors of <paramref name="option"/> and the option itself, outermost first.</summary>
    public IReadOnlyList<ScopeOption> TrailOf(ScopeOption option)
    {
        ArgumentNullException.ThrowIfNull(option);

        List<ScopeOption> trail = [option];

        for (ScopeOption? parent = Find(option.ParentId); parent is not null; parent = Find(parent.ParentId))
        {
            trail.Insert(0, parent);
        }

        return trail;
    }

    private bool Move(Guid scopeId)
    {
        if (scopeId == scopeContext.ScopeId || Find(scopeId) is null)
        {
            return false;
        }

        scopeContext.Set(scopeId);

        return true;
    }

    /// <summary>
    /// The directory lists by depth, then name; an indented list needs each scope directly above its children.
    /// </summary>
    private static List<ScopeOption> InTreeOrder(IReadOnlyList<ScopeOption> listed)
    {
        ILookup<Guid?, ScopeOption> children = listed.ToLookup(option => option.ParentId);
        List<ScopeOption> ordered = new(listed.Count);
        Stack<ScopeOption> pending = new(children[null].Reverse());

        while (pending.TryPop(out ScopeOption? option))
        {
            ordered.Add(option);

            foreach (ScopeOption child in children[option.Id].Reverse())
            {
                pending.Push(child);
            }
        }

        return ordered;
    }

    private ScopeOption? Find(Guid? scopeId) =>
        scopeId is { } id ? Options.FirstOrDefault(option => option.Id == id) : null;
}
