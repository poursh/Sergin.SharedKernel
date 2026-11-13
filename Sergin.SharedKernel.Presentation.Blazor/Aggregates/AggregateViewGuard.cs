using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Commands.Types;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// The view configuration mistakes that refuse host start, each naming the configuration and the offending type,
/// property or route. <see cref="AggregateViewRegistry.Build"/> runs every check its requests can answer (two
/// configurations for one root; a command, detail, list or form naming a request not the root's; routes promising a
/// page with no query or command behind it; a schedulable command a detail view cannot build from the id; a field
/// override on an unknown property); <see cref="Ensure"/> checks the configured
/// routes against the routable components, once the UI assemblies are known.
/// </summary>
public static class AggregateViewGuard
{
    private static readonly string emptyId = Guid.Empty.ToString("D", CultureInfo.InvariantCulture);

    /// <summary>Every configured route must match a routable component's <c>[Route]</c> template in <paramref name="uiAssemblies"/>.</summary>
    public static void Ensure(AggregateViewRegistry views, IEnumerable<Assembly> uiAssemblies)
    {
        ArgumentNullException.ThrowIfNull(views);
        ArgumentNullException.ThrowIfNull(uiAssemblies);

        HashSet<string> templates = new(
            uiAssemblies
                .Distinct()
                .SelectMany(assembly => assembly.GetExportedTypes())
                .Where(typeof(IComponent).IsAssignableFrom)
                .SelectMany(component => component.GetCustomAttributes<RouteAttribute>(inherit: false))
                .Select(route => Normalize(route.Template)),
            StringComparer.OrdinalIgnoreCase);

        string[] missing =
        [
            .. views.All.SelectMany(model => RoutesOf(model.Routes)
                .Where(route => !templates.Contains(Normalize(route)))
                .Select(route => $"  {model.Configuration.FullName} ({model.Root.Name}): \"{route}\"")),
            .. views.All.SelectMany(model => model.Children.SelectMany(child => RoutesOf(child.Routes)
                .Where(route => !templates.Contains(Normalize(route)))
                .Select(route => $"  {model.Configuration.FullName} ({model.Root.Name}, child {child.Child.Name}): \"{route}\""))),
        ];

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                "Aggregate view configurations name routes no routable component declares with @page; a generated view's "
                + "links would 404:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, missing));
        }
    }

    /// <summary>A transition or delete is sent for one record: its constructor must take the id and nothing else.</summary>
    private static void EnsureIdOnly(DeclaredView view, IReadOnlyCollection<CommandView> commands)
    {
        CommandView? offender = commands.FirstOrDefault(command => command.Role is CommandRole.Transition or CommandRole.Delete && command.BuildFromId is null);

        if (offender is not null)
        {
            throw Refuse(
                view,
                $"{offender.Request.FullName} is a {offender.Role} command, which a generated view sends from the record's id alone, but its "
                + "constructor takes more than the id. Give it a constructor of only the id, or model it as an update or set-property command.");
        }
    }

    /// <summary>
    /// A schedulable command's Schedule… and View schedules entries build the command from the record's id, so a shown one
    /// must be id-only (a list's bulk schedule already skips one that is not).
    /// </summary>
    private static void EnsureSchedulableFromId(DeclaredView view, IReadOnlyCollection<CommandView> commands)
    {
        CommandView? offender = commands.FirstOrDefault(command => command is { ScheduleName: not null, BuildFromId: null, Hidden: false });

        if (offender is not null)
        {
            throw Refuse(
                view,
                $"{offender.Request.FullName} is Schedulable(\"{offender.ScheduleName}\"), and a generated detail view schedules it from the "
                + "record's id alone, but its constructor takes more than the id. Give it a constructor of only the id, or "
                + $"Command<{offender.Request.Name}>().Hidden() it.");
        }
    }

    /// <summary>GetOne and audit queries are sent from the record's id alone; an overview takes no arguments.</summary>
    private static void EnsureQueryShapes(DeclaredView view, Type? getOne, Type? overview, Type? entityOverview, Type? audit)
    {
        if (new[] { getOne, entityOverview, audit }.OfType<Type>().FirstOrDefault(query => !RequestFactory.IsIdOnly(query)) is { } query)
        {
            throw Refuse(
                view,
                $"{query.FullName} is loaded from the record's id alone, but its constructor takes more than the id. Give it a constructor of only the id.");
        }

        if (overview is not null && !RequestFactory.IsParameterless(overview))
        {
            throw Refuse(view, $"{overview.FullName} is an overview over the whole collection, so its constructor must take no arguments.");
        }
    }

    internal static void EnsureOnePerRoot(IReadOnlyCollection<DeclaredView> declared)
    {
        string[] duplicates =
        [
            .. declared
                .GroupBy(view => view.Builder.Root)
                .Where(group => group.Count() > 1)
                .Select(group => $"{group.Key.FullName} ({string.Join(", ", group.Select(view => view.Configuration.FullName))})"),
        ];

        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"More than one aggregate view configuration is declared for: {string.Join("; ", duplicates)}. "
                + "Declare each root's views in exactly one IAggregateViewConfiguration<T>.");
        }
    }

    /// <summary>What the configuration named must be the root's: its commands, its GetOne and list records, its forms; its routes need their queries.</summary>
    internal static void EnsureConfigured(
        DeclaredView view,
        IReadOnlyCollection<CommandView> commands,
        Type? getOne,
        Type? getOneResponse,
        Type? list,
        Type? listItem,
        Type? create,
        Type? update,
        Type? overview,
        Type? entityOverview,
        Type? audit)
    {
        AggregateViewBuilder builder = view.Builder;

        EnsureCommandsOfRoot(view, commands);
        EnsureIdOnly(view, commands);
        EnsureSchedulableFromId(view, commands);
        EnsureQueryShapes(view, getOne, overview, entityOverview, audit);
        EnsureRecord(view, builder.Root.Name, builder.DetailFields?.Record, getOneResponse, "Detail", "GetOne query's response");
        EnsureRecord(view, builder.Root.Name, builder.ListFields?.Record, listItem, "List", "list query's item");

        if (builder.Forms.Keys.FirstOrDefault(command => command != create && command != update) is { } form)
        {
            throw Refuse(view, $"Form<{form.Name}>() names {form.FullName}, which is not {builder.Root.Name}'s create or update command.");
        }

        AggregateViewRoutes routes = builder.DeclaredRoutes;

        if ((routes.Detail is not null || routes.Edit is not null) && getOne is null)
        {
            throw Refuse(view, $"its routes promise a {builder.Root.Name} detail page, but no IGetOneQuery<{builder.Root.Name}, …> exists to load it.");
        }

        if (routes.List is not null && list is null)
        {
            throw Refuse(view, $"its routes promise a {builder.Root.Name} list page, but no IListOf<{builder.Root.Name}> query exists to fill it.");
        }

        if (routes.Create is not null && create is null)
        {
            throw Refuse(view, $"its routes promise a {builder.Root.Name} create form, but no ICreateCommand<{builder.Root.Name}, …> exists to send.");
        }

        if (routes.Edit is not null && update is null)
        {
            throw Refuse(view, $"its routes promise a {builder.Root.Name} edit form, but no IUpdateCommand<{builder.Root.Name}, …> exists to send.");
        }
    }

    private static void EnsureCommandsOfRoot(DeclaredView view, IReadOnlyCollection<CommandView> commands)
    {
        Type root = view.Builder.Root;

        foreach (Type request in view.Builder.Commands.Keys)
        {
            Type? target = CommandTypes.TargetOf(request)?.Root;

            if (target != root)
            {
                throw Refuse(
                    view,
                    $"Command<{request.Name}>() names {request.FullName}, which is typed to "
                    + $"{target?.FullName ?? "no aggregate"}, not to {root.FullName}.");
            }

            if (commands.All(command => command.Request != request))
            {
                throw Refuse(
                    view,
                    $"Command<{request.Name}>() names {request.FullName}, which is no create, update, delete, transition, "
                    + $"set-property or move command on {root.Name} itself, so no action shows it.");
            }
        }
    }

    private static void EnsureRecord(DeclaredView view, string owner, Type? configured, Type? expected, string method, string what)
    {
        if (configured is not null && configured != expected)
        {
            throw Refuse(
                view,
                $"{method}<{configured.Name}>() names {configured.FullName}, which is not {owner}'s {what} "
                + $"({expected?.FullName ?? "there is none"}).");
        }
    }

    // ---- Edit modes, disabled actions, child entities ----

    /// <summary>
    /// A set-property command is edited on a page of its own, which only a child entity has so far: the root's is refused
    /// in any mode, a child's unless declared <c>Edit(SerginEditMode.Page)</c> (dialog and inline editing come later).
    /// </summary>
    internal static void EnsureEditable(DeclaredView view, Type request, CommandRole role, bool child, SerginEditMode mode)
    {
        if (role != CommandRole.SetProperty)
        {
            return;
        }

        if (!child)
        {
            throw Refuse(
                view,
                $"{request.FullName} is a set-property command on {view.Builder.Root.Name} itself, which a generated view cannot offer yet "
                + "in any edit mode; only a child entity's set-property command is supported, declared Edit(SerginEditMode.Page).");
        }

        if (mode != SerginEditMode.Page)
        {
            throw Refuse(
                view,
                $"{request.FullName} is a set-property command, and edit mode {mode} is not supported yet; "
                + $"declare Command<{request.Name}>().Edit(SerginEditMode.Page).");
        }
    }

    /// <summary><c>DisabledWhen&lt;TRecord&gt;</c> reads a record the command's views hold: a list item or GetOne response of the root or child.</summary>
    internal static void EnsureDisabledRecord(DeclaredView view, Type request, Type disabledRecord, IReadOnlyCollection<Type?> records)
    {
        if (!records.Contains(disabledRecord))
        {
            string known = string.Join(", ", records.OfType<Type>().Select(type => type.Name));

            throw Refuse(
                view,
                $"Command<{request.Name}>().DisabledWhen<{disabledRecord.Name}>() reads {disabledRecord.FullName}, which is no record "
                + $"of the command's views ({(known.Length == 0 ? "there are none" : known)}).");
        }
    }

    /// <summary>A command that edits a child on a page of its own: its update, or one declared <c>Edit(SerginEditMode.Page)</c>.</summary>
    internal static bool IsEditCommand(CommandView command) =>
        command.Role == CommandRole.Update || command.EditMode == SerginEditMode.Page;

    /// <summary>
    /// What a child's declaration names must be its own: its commands, GetOne response and list item, its forms; its
    /// routes need their query or command; it has at most one edit command; a remove or transition is sent from the ids alone.
    /// </summary>
    internal static void EnsureChild(
        DeclaredView view,
        Type child,
        ChildViewBuilder declared,
        IReadOnlyCollection<CommandView> commands,
        Type? getOne,
        Type? getOneResponse,
        Type? listItem,
        Type? add,
        CommandView? editCommand)
    {
        Type root = view.Builder.Root;

        EnsureChildCommands(view, child, declared, commands);

        if (commands.FirstOrDefault(command => command.Role is CommandRole.Transition or CommandRole.RemoveChild && command.BuildFromIds is null) is { } offender)
        {
            throw Refuse(
                view,
                $"{offender.Request.FullName} is a {offender.Role} command, which a generated view sends from the root's id and the "
                + $"child's alone, but its constructor is not (RootId, Id). Give it that constructor, or model it as an update or set-property command.");
        }

        if ((declared.ListFields as IListFilterDeclarations)?.FilterComponents.Keys.FirstOrDefault() is { } filter)
        {
            throw Refuse(
                view,
                $"List<…>(list => list.Filter(\"{filter}\", …)) on Child<{child.Name}> has no effect: a child list section has no filter panel. Remove it.");
        }

        if (getOne is not null && !RequestFactory.IsChildIdOnly(getOne))
        {
            throw Refuse(view, $"{getOne.FullName} is loaded from the root's id and the child's alone, but its constructor is not (RootId, Id).");
        }

        EnsureRecord(view, child.Name, declared.DetailFields?.Record, getOneResponse, "Detail", "GetOne query's response");
        EnsureRecord(view, child.Name, declared.ListFields?.Record, listItem, "List", "list query's item");

        if (declared.Forms.Keys.FirstOrDefault(command => command != add && command != editCommand?.Request) is { } form)
        {
            throw Refuse(view, $"Form<{form.Name}>() on Child<{child.Name}> names {form.FullName}, which is not {child.Name}'s add or edit command.");
        }

        CommandView[] editors = [.. commands.Where(IsEditCommand)];

        if (editors.Length > 1)
        {
            throw Refuse(
                view,
                $"{child.Name} has more than one edit command ({string.Join(", ", editors.Select(command => command.Request.FullName))}); "
                + "a generated child page edits with exactly one: its update, or one command declared Edit(SerginEditMode.Page).");
        }

        ChildViewRoutes routes = declared.DeclaredRoutes;

        if (editCommand is not null && routes.Edit is null)
        {
            throw Refuse(
                view,
                $"{editCommand.Request.FullName} edits {child.Name} on a page of its own, but Child<{child.Name}>().Routes(...) names no edit route.");
        }

        if (editCommand is null && routes.Edit is not null)
        {
            throw Refuse(
                view,
                $"its routes promise a {child.Name} edit page, but {child.Name} has no IUpdateCommand<{root.Name}, …, {child.Name}, …> "
                + "and no command declared Edit(SerginEditMode.Page) to send.");
        }

        EnsureChildRoutesCarryIds(view, child, routes);

        if (routes.Create is not null && add is null)
        {
            throw Refuse(view, $"its routes promise a {child.Name} create form, but no IAddChildCommand<{root.Name}, …, {child.Name}, …> exists to send.");
        }

        if (routes.Detail is not null && getOne is null)
        {
            throw Refuse(view, $"its routes promise a {child.Name} detail page, but no IChildGetOneQuery<{root.Name}, …, {child.Name}, …> exists to load it.");
        }
    }

    // Sampled with two distinct ids: a route that drops one (or writes the child's id where the root's belongs) would link to the wrong page.
    private static void EnsureChildRoutesCarryIds(DeclaredView view, Type child, ChildViewRoutes routes)
    {
        var rootId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var childId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        string root = rootId.ToString();
        string childText = childId.ToString();

        (string Name, string? Route, bool NeedsChild)[] sampled =
        [
            ("create", routes.Create?.Invoke(rootId), false),
            ("detail", routes.Detail?.Invoke(rootId, childId), true),
            ("edit", routes.Edit?.Invoke(rootId, childId), true),
        ];

        foreach ((string name, string? route, bool needsChild) in sampled)
        {
            if (route is not null
                && (!route.Contains(root, StringComparison.OrdinalIgnoreCase)
                    || needsChild && !route.Contains(childText, StringComparison.OrdinalIgnoreCase)))
            {
                throw Refuse(
                    view,
                    $"Child<{child.Name}>().Routes(...) {name} route builds \"{route}\" from the root id {root}"
                    + $"{(needsChild ? $" and the child id {childText}" : string.Empty)}, and it must carry "
                    + $"{(needsChild ? "both" : "the root id")}.");
            }
        }
    }

    private static void EnsureChildCommands(DeclaredView view, Type child, ChildViewBuilder declared, IReadOnlyCollection<CommandView> commands)
    {
        Type root = view.Builder.Root;

        foreach (Type request in declared.Commands.Keys)
        {
            CommandTarget? target = CommandTypes.TargetOf(request);

            if (target is null || target.Root != root || !AggregateTargets.ChildrenNamedBy(request, target).Contains(child))
            {
                throw Refuse(
                    view,
                    $"Command<{request.Name}>() on Child<{child.Name}> names {request.FullName}, which is typed to "
                    + $"{(target is null ? "no aggregate" : target.Root.FullName)}"
                    + $"{(target?.Child is { } other ? $" (child {other.Name})" : string.Empty)}, not to {child.Name} of {root.Name}.");
            }

            if (commands.All(command => command.Request != request))
            {
                throw Refuse(
                    view,
                    $"Command<{request.Name}>() names {request.FullName}, which is no add, remove, update, transition or set-property "
                    + $"command on {child.Name}, so no action shows it.");
            }
        }
    }

    private static InvalidOperationException Refuse(DeclaredView view, string problem) =>
        new($"Aggregate view configuration {view.Configuration.FullName}: {problem}");

    private static IEnumerable<string> RoutesOf(ChildViewRoutes routes)
    {
        string?[] all = [routes.Create?.Invoke(Guid.Empty), routes.Detail?.Invoke(Guid.Empty, Guid.Empty), routes.Edit?.Invoke(Guid.Empty, Guid.Empty)];
        return all.OfType<string>();
    }

    private static IEnumerable<string> RoutesOf(AggregateViewRoutes routes)
    {
        string?[] all = [routes.List, routes.Create, routes.Detail?.Invoke(Guid.Empty), routes.Edit?.Invoke(Guid.Empty)];
        return all.OfType<string>();
    }

    /// <summary>A route without its query string, every <c>{…:guid}</c> parameter written as the empty id, so a template and a built route compare.</summary>
    private static string Normalize(string route)
    {
        int query = route.IndexOf('?', StringComparison.Ordinal);
        string path = query < 0 ? route : route[..query];

        return string.Join('/', path.Split('/').Select(segment =>
            segment.StartsWith('{') && segment.EndsWith(":guid}", StringComparison.OrdinalIgnoreCase) ? emptyId : segment));
    }
}
