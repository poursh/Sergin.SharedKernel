using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;
using Sergin.SharedKernel.Presentation.Blazor.Pages;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views;

/// <summary>
/// One child entity's list as a titled, collapsible section of its root's detail view, generated from its
/// <see cref="ChildViewModel"/>: the title with the list's total, an Add action linking to the child's create route
/// (guarded by the add command's permission), and the table (<see cref="GeneratedChildListBody{TItem}"/>, closed over the
/// list item type).
/// </summary>
/// <remarks>
/// Removing a child changes the root, so the section takes the root's <see cref="Version"/> from the detail view and
/// tells it through <see cref="OnChanged"/> when a command moved it, for it to read the root again. The section reloads
/// its own rows after each command; <see cref="ReloadAsync"/> is for the root's reload.
/// </remarks>
public sealed partial class SerginGeneratedChildSection
{
    private IGeneratedChildListBody? body;
    private int? total;

    [Parameter]
    [EditorRequired]
    public AggregateViewModel Model { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public ChildViewModel Child { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public Guid RootId { get; set; }

    /// <summary>The root's version, as its detail view last read it; null until it has one.</summary>
    [Parameter]
    public RowVersion? Version { get; set; }

    [Parameter]
    public EventCallback OnChanged { get; set; }

    // The add command's permission is the one the new child is checked on; the root's aggregate decides whether it is any scope.
    private IReadOnlyList<SerginPageAction> Actions => [.. AddAction()];

    private RenderFragment Body => builder =>
    {
        builder.OpenComponent(0, Child.ListBodyType ?? throw new InvalidOperationException(
            $"{Child.Child.FullName} has no list query, so its section has nothing to show. Add an IChildListOf<{Model.Root.Name}, …, {Child.Child.Name}>."));
        builder.AddAttribute(1, nameof(GeneratedChildListBody<object>.Child), Child);
        builder.AddAttribute(2, nameof(GeneratedChildListBody<object>.RootId), RootId);
        builder.AddAttribute(3, nameof(GeneratedChildListBody<object>.Version), (object?)Version);
        builder.AddAttribute(4, nameof(GeneratedChildListBody<object>.OnChanged), OnChanged);
        builder.AddAttribute(5, nameof(GeneratedChildListBody<object>.TotalChanged), EventCallback.Factory.Create<int>(this, count => total = count));
        builder.AddComponentReferenceCapture(6, component => body = (IGeneratedChildListBody)component);
        builder.CloseComponent();
    };

    public Task ReloadAsync() => body is null ? Task.CompletedTask : body.ReloadAsync();

    private IEnumerable<SerginPageAction> AddAction()
    {
        if (Child.Routes.Create is not { } create
            || Child.Commands.FirstOrDefault(command => command is { Role: CommandRole.AddChild, Hidden: false }) is not { } command)
        {
            yield break;
        }

        yield return new SerginPageAction(
            command.Label,
            command.Icon,
            SerginActionKind.Primary,
            Href: create(RootId),
            Permission: command.Permissions.FirstOrDefault(),
            AnyScope: command.AnyScope);
    }
}
