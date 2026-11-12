using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Commands.Types;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// The view configuration mistakes that refuse host start, each naming the configuration and the offending type,
/// property or route. <see cref="AggregateViewRegistry.Build"/> runs every check its requests can answer (two
/// configurations for one root; a command, detail, list or form naming a request not the root's; routes promising a
/// page with no query behind it; a field override on an unknown property); <see cref="Ensure"/> checks the configured
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
        Type? update)
    {
        AggregateViewBuilder builder = view.Builder;

        EnsureCommandsOfRoot(view, commands);
        EnsureRecord(view, builder.DetailFields?.Record, getOneResponse, "Detail", "GetOne query's response");
        EnsureRecord(view, builder.ListFields?.Record, listItem, "List", "list query's item");

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

    private static void EnsureRecord(DeclaredView view, Type? configured, Type? expected, string method, string what)
    {
        if (configured is not null && configured != expected)
        {
            throw Refuse(
                view,
                $"{method}<{configured.Name}>() names {configured.FullName}, which is not {view.Builder.Root.Name}'s {what} "
                + $"({expected?.FullName ?? "there is none"}).");
        }
    }

    private static InvalidOperationException Refuse(DeclaredView view, string problem) =>
        new($"Aggregate view configuration {view.Configuration.FullName}: {problem}");

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
