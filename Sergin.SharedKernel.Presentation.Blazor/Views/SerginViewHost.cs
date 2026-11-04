namespace Sergin.SharedKernel.Presentation.Blazor.Views;

/// <summary>Where a view is shown, which decides where its title, trail and header buttons go.</summary>
public enum SerginViewHostKind
{
    /// <summary>The view is a routable page's content: it owns the page title and the header row.</summary>
    Page,

    /// <summary>The view sits inside another view or component: it renders as a titled section and claims no header.</summary>
    Embedded,

    /// <summary>
    /// The view fills a pane of a page that owns the page title and header row — the content side of a
    /// <c>SerginTree</c>. It renders like a page's view, but its header buttons go to the owner's page header
    /// (<see cref="SerginViewHost.HeaderSlot"/>) and it renders no page title or trail.
    /// </summary>
    Pane,
}

/// <summary>
/// What a view knows about the thing showing it, cascaded by <see cref="SerginPageLayout"/> and <c>SerginTree</c> and re-cascaded by every
/// <see cref="SerginView"/> to its own body as <see cref="SerginViewHostKind.Embedded"/>, so a view nested in
/// another never takes over the page header.
/// </summary>
/// <remarks>
/// A view calls <see cref="LeaveAsync"/> when it is done — a record deleted, a form saved or cancelled — instead of
/// navigating itself, so a future dialog host can close where a page navigates. A view declares its cascading
/// parameter as <c>SerginViewHost Host { get; set; } = SerginViewHost.Unhosted;</c>: outside any host it renders as
/// embedded, and leaving fails naming the mistake. A view that changed its record and stays open (renamed, moved)
/// calls <see cref="ChangedAsync"/>, so a host that lists the record — a tree page — can read it again.
/// </remarks>
public sealed class SerginViewHost
{
    private readonly Func<string, Task> leave;
    private readonly Func<Task>? changed;

    public SerginViewHost(SerginViewHostKind kind, Func<string, Task> leave)
        : this(kind, leave, headerSlot: null)
    {
    }

    /// <param name="kind">Where the view is shown.</param>
    /// <param name="leave">What leaving the view does.</param>
    /// <param name="headerSlot">
    /// The slot a <see cref="SerginViewHostKind.Pane"/> view writes its header buttons into; required for a pane.
    /// </param>
    /// <param name="changed">What the host does when the view changed its record and stays open; null for nothing.</param>
    public SerginViewHost(SerginViewHostKind kind, Func<string, Task> leave, SerginHeaderSlot? headerSlot, Func<Task>? changed = null)
    {
        ArgumentNullException.ThrowIfNull(leave);

        if (kind == SerginViewHostKind.Pane && headerSlot is null)
        {
            throw new ArgumentException("A pane host needs the slot its view's header buttons render into.", nameof(headerSlot));
        }

        Kind = kind;
        this.leave = leave;
        this.changed = changed;
        HeaderSlot = headerSlot;
    }

    /// <summary>The host of a view rendered outside any <see cref="SerginPageLayout"/>: embedded, and unable to leave.</summary>
    public static SerginViewHost Unhosted { get; } = new(
        SerginViewHostKind.Embedded,
        _ => throw new InvalidOperationException("A view tried to leave, but it is not rendered inside a SerginPageLayout or another host."));

    public SerginViewHostKind Kind { get; }

    public bool IsPage => Kind == SerginViewHostKind.Page;

    public bool IsPane => Kind == SerginViewHostKind.Pane;

    /// <summary>The slot a pane view's header buttons render into; null for any other host.</summary>
    public SerginHeaderSlot? HeaderSlot { get; }

    /// <summary>Ends the view: a page navigates to <paramref name="href"/>; a tree's pane reads the tree again, then navigates.</summary>
    public Task LeaveAsync(string href)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(href);

        return leave(href);
    }

    /// <summary>The view changed its record and stays open: a tree page reads its tree again; a page does nothing.</summary>
    public Task ChangedAsync() => changed?.Invoke() ?? Task.CompletedTask;

    /// <summary>The host a view hands its own body: embedded, leaving and reporting changes the way this host does.</summary>
    public SerginViewHost Embedded() =>
        Kind == SerginViewHostKind.Embedded ? this : new SerginViewHost(SerginViewHostKind.Embedded, leave, headerSlot: null, changed);
}
