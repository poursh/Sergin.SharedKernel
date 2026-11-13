using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// Joins one child entity's declaration with the typed requests that name it into a <see cref="ChildViewModel"/>. The
/// fields, forms and command views come from the root's join, so a child and a root read their requests the same way;
/// every refusal names the configuration.
/// </summary>
internal sealed class ChildViewModelJoin(AggregateViewModelJoin join, Type? rootGetOneResponse, Type? rootListItem)
{
    private readonly DeclaredView view = join.View;

    /// <summary>The model of <paramref name="child"/>, from the root's typed requests that name it.</summary>
    public ChildViewModel Build(Type child, Type[] requests)
    {
        Type root = view.Builder.Root;

        if (view.Builder.Children.GetValueOrDefault(child) is not { Singular: { } singular } declared)
        {
            throw join.Refuse(
                $"{child.FullName} is a child entity of {root.Name} with typed requests, but its view declares no name. "
                + $"Declare it with Child<{child.Name}>(child => child.Named(...)).");
        }

        Type? getOne = join.Single(requests, typeof(IChildGetOneQuery<,,,>));
        Type? list = join.Single(requests, typeof(IChildListOf<,,>));
        Type? add = join.Single(requests, typeof(IAddChildCommand<,,,>));
        Type? getOneResponse = getOne is null ? null : AggregateViewModelJoin.ResponseOf(getOne);
        Type? listItem = list is null ? null : AggregateViewModelJoin.ListItemOf(list);

        CommandView[] commands =
        [
            .. join.Commands(requests, declared, singular, child: true, [getOneResponse, listItem, rootGetOneResponse, rootListItem]),
        ];
        CommandView? editCommand = EditCommandOf(commands);

        AggregateViewGuard.EnsureChild(view, child, declared, commands, getOne, getOneResponse, listItem, add, editCommand);

        IReadOnlyList<FieldView> detailFields = join.Fields(getOneResponse, join.CategoryOf(getOne), declared.DetailFields, out string? detailTitle);
        IReadOnlyList<FieldView> listFields = join.Fields(listItem, join.CategoryOf(list), declared.ListFields, out string? listTitle);
        var listDeclarations = declared.ListFields as IListFilterDeclarations;

        return new ChildViewModel(
            child,
            singular,
            declared.SectionTitle ?? singular,
            declared.DeclaredRoutes,
            AggregateViewModelJoin.Request(list, request => RequestFactory.ForChildList(request, RootIdOf(request))),
            listItem,
            listFields,
            list is null ? null : AggregateViewModelJoin.ListFieldSetOf(list),
            listTitle,
            listDeclarations?.SearchPlaceholder,
            listDeclarations?.EmptyText,
            listItem is null ? null : AggregateViewModelJoin.ArgumentsReader(listItem),
            AggregateViewModelJoin.Request(getOne, RequestFactory.ForChildId),
            detailFields,
            detailTitle,
            getOneResponse is null ? null : AggregateViewModelJoin.ArgumentsReader(getOneResponse),
            AggregateViewModelJoin.Request(add, RequestFactory.ForRootForm),
            join.FormFields(add, declared, getOneResponse),
            add is null ? null : declared.Forms.GetValueOrDefault(add)?.DeclaredTitle,
            AggregateViewModelJoin.Request(editCommand?.Request, RequestFactory.ForChildForm),
            join.FormFields(editCommand?.Request, declared, getOneResponse),
            editCommand is null ? null : declared.Forms.GetValueOrDefault(editCommand.Request)?.DeclaredTitle,
            editCommand,
            commands,
            listItem is null ? null : typeof(GeneratedChildListBody<>).MakeGenericType(listItem));
    }

    /// <summary>The child's edit command: its update, or the command declared <c>Edit(SerginEditMode.Page)</c> (the guard refuses a second).</summary>
    private static CommandView? EditCommandOf(IReadOnlyCollection<CommandView> commands) =>
        commands.FirstOrDefault(AggregateViewGuard.IsEditCommand);

    /// <summary>The <c>TRootId</c> of the <c>IChildListOf</c> a child list query implements: the type its constructor takes the root's id as.</summary>
    private static Type RootIdOf(Type listQuery) =>
        listQuery.GetInterfaces()
            .First(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IChildListOf<,,>))
            .GetGenericArguments()[1];
}
