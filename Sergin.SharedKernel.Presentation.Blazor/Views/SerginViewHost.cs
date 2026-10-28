namespace Sergin.SharedKernel.Presentation.Blazor.Views;

/// <summary>Where a view is shown, which decides where its title, trail and header buttons go.</summary>
public enum SerginViewHostKind
{
    /// <summary>The view is a routable page's content: it owns the page title and the header row.</summary>
    Page,

    /// <summary>The view sits inside another view or component: it renders as a titled section and claims no header.</summary>
    Embedded,
}

/// <summary>
/// What a view knows about the thing showing it, cascaded by <see cref="SerginPage"/> and re-cascaded by every
/// <see cref="SerginView"/> to its own body as <see cref="SerginViewHostKind.Embedded"/>, so a view nested in
/// another never takes over the page header.
/// </summary>
/// <remarks>
/// A view calls <see cref="LeaveAsync"/> when it is done — a record deleted, a form saved or cancelled — instead of
/// navigating itself, so a future dialog host can close where a page navigates. A view declares its cascading
/// parameter as <c>SerginViewHost Host { get; set; } = SerginViewHost.Unhosted;</c>: outside any host it renders as
/// embedded, and leaving fails naming the mistake.
/// </remarks>
public sealed class SerginViewHost
{
    private readonly Func<string, Task> leave;

    public SerginViewHost(SerginViewHostKind kind, Func<string, Task> leave)
    {
        ArgumentNullException.ThrowIfNull(leave);

        Kind = kind;
        this.leave = leave;
    }

    /// <summary>The host of a view rendered outside any <see cref="SerginPage"/>: embedded, and unable to leave.</summary>
    public static SerginViewHost Unhosted { get; } = new(
        SerginViewHostKind.Embedded,
        _ => throw new InvalidOperationException("A view tried to leave, but it is not rendered inside a SerginPage or another host."));

    public SerginViewHostKind Kind { get; }

    public bool IsPage => Kind == SerginViewHostKind.Page;

    /// <summary>Ends the view: a page navigates to <paramref name="href"/>.</summary>
    public Task LeaveAsync(string href)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(href);

        return leave(href);
    }

    /// <summary>The host a view hands its own body: embedded, leaving the way this host does.</summary>
    public SerginViewHost Embedded() =>
        Kind == SerginViewHostKind.Embedded ? this : new SerginViewHost(SerginViewHostKind.Embedded, leave);
}
