using System.Linq.Expressions;
using System.Reflection;
using MudBlazor;
using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Configuration;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Presentation.Blazor.Pages;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// Joins one view configuration with its root's typed requests, their configurations, the root's features and texts,
/// into the root's <see cref="AggregateViewModel"/>; every refusal names the configuration.
/// </summary>
internal sealed class AggregateViewModelJoin(
    DeclaredView view,
    CommandConfigurationRegistry commands,
    AggregateFeatureRegistry aggregates,
    TextCatalog texts,
    AggregateTargets targets)
{
    private readonly AggregateViewBuilder builder = view.Builder;
    private readonly Type root = view.Builder.Root;

    public AggregateViewModel Build()
    {
        Type[] requests =
        [
            .. commands.RequestTypes.Where(type => CommandTypes.TargetOf(type)?.Root == root).OrderBy(type => type.FullName, StringComparer.Ordinal),
        ];

        TextDefinition singular = aggregates.NameOf(root) ?? throw Refuse(
            $"{root.FullName} declares no name. Add Named(...) to its aggregate feature configuration: it is the detail and form title.");

        Type? getOne = Single(requests, typeof(IGetOneQuery<,>));
        Type? list = Single(requests, typeof(IListOf<>));
        Type? create = Single(requests, typeof(ICreateCommand<,>));
        Type? update = Single(requests, typeof(IUpdateCommand<,>));
        Type? getOneResponse = getOne is null ? null : ResponseOf(getOne);
        Type? listItem = list is null ? null : ListItemOf(list);
        CommandView[] commandViews = [.. Commands(requests)];

        AggregateViewGuard.EnsureConfigured(view, commandViews, getOne, getOneResponse, list, listItem, create, update);

        IReadOnlyList<FieldView> detailFields = Fields(getOneResponse, CategoryOf(getOne), builder.DetailFields, out string? detailTitle);
        IReadOnlyList<FieldView> listFields = Fields(listItem, CategoryOf(list), builder.ListFields, out string? listTitle);
        ListFieldSet? listFieldSet = list is null ? null : ListFieldSetOf(list);
        IReadOnlyList<ListFilterView> filters = Filters(listFieldSet, builder.ListFields as IListFilterDeclarations);

        Type? overview = Single(requests, typeof(IOverviewQuery<>));
        Type? audit = Single(requests, typeof(IAuditQuery<,>));

        return new AggregateViewModel(
            root,
            view.Configuration,
            view.Schema,
            builder.ListTitle ?? singular,
            singular,
            builder.DeclaredRoutes,
            aggregates.For(root),
            getOne,
            list,
            overview,
            audit,
            create,
            update,
            commandViews,
            detailFields,
            listFields,
            listFieldSet,
            FormFields(create, getOneResponse),
            FormFields(update, getOneResponse),
            new Dictionary<SerginViewSlot, Type>(builder.Slots),
            commandViews.Any(command => command.ScheduleName is not null),
            detailTitle,
            listTitle,
            filters,
            Invoker(getOne),
            IdBuilder(getOne),
            Invoker(list),
            list is null ? null : RequestFactory.ForList(list),
            Invoker(overview),
            IdBuilder(overview),
            Invoker(audit),
            IdBuilder(audit),
            Invoker(create),
            create is null ? null : RequestFactory.ForForm(create),
            Invoker(update),
            update is null ? null : RequestFactory.ForForm(update));
    }

    private static RequestInvoker? Invoker(Type? request) => request is null ? null : RequestInvoker.For(request);

    private static Func<Guid, object>? IdBuilder(Type? request) =>
        request is not null && RequestFactory.IsIdOnly(request) ? RequestFactory.ForId(request) : null;

    private InvalidOperationException Refuse(string problem) =>
        new($"Aggregate view configuration {view.Configuration.FullName}: {problem}");

    private Type? Single(Type[] requests, Type openInterface)
    {
        Type[] found = [.. requests.Where(request => Implements(request, openInterface))];

        if (found.Length > 1)
        {
            throw Refuse(
                $"{root.Name} has more than one {openInterface.Name[..openInterface.Name.IndexOf('`', StringComparison.Ordinal)]} "
                + $"({string.Join(", ", found.Select(type => type.FullName))}); a generated view needs exactly one.");
        }

        return found.FirstOrDefault();
    }

    private TextCategory? CategoryOf(Type? request) =>
        (request is null ? null : commands.CategoryOf(request)) ?? aggregates.TextsOf(root);

    // ---- Commands ----

    private IEnumerable<CommandView> Commands(Type[] requests)
    {
        foreach (Type request in requests.Where(type => !CommandTypes.IsSystem(type) && IsCommand(type)))
        {
            if (RoleOf(request) is not { } role)
            {
                continue;
            }

            builder.Commands.TryGetValue(request, out CommandViewBuilder? declared);
            CommandSettings settings = commands.For(request);
            (TextDefinition? defaultLabel, string? defaultIcon) = DefaultsOf(role);
            TextDefinition? label = declared?.DeclaredLabel ?? defaultLabel;

            if (label is null && declared is not { IsHidden: true })
            {
                throw Refuse(
                    $"{request.FullName} is a {role} command with no default label. Declare one with "
                    + $"Command<{request.Name}>().Label(...), or Hidden() it.");
            }

            yield return new CommandView(
                request,
                role,
                label ?? aggregates.NameOf(root)!,
                declared?.DeclaredIcon ?? defaultIcon,
                KindOf(role),
                settings.RequiredPermissions,
                settings.PermissionScope == PermissionScope.Any,
                settings.RequiresExpectedVersion,
                settings.ScheduleName,
                CommandTypes.IsSelectable(request),
                declared?.DeclaredSucceededText,
                declared?.ConfirmTitle,
                declared?.ConfirmText,
                declared?.IsHidden ?? false,
                [.. declared?.MenuItems ?? []],
                RequestInvoker.For(request),
                RequestFactory.IsIdOnly(request) ? RequestFactory.ForId(request) : null);
        }
    }

    /// <summary>The role of a command on the root itself; null for a child, collection or unlisted effect.</summary>
    private static CommandRole? RoleOf(Type request)
    {
        IReadOnlyList<Type> effects = CommandTypes.EffectsOf(request);

        return effects switch
        {
            _ when effects.Contains(typeof(ICreateCommand<,>)) => CommandRole.Create,
            _ when effects.Contains(typeof(IUpdateCommand<,>)) => CommandRole.Update,
            _ when effects.Contains(typeof(IDeleteCommand<,>)) => CommandRole.Delete,
            _ when effects.Contains(typeof(ITransitionCommand<,>)) => CommandRole.Transition,
            _ when effects.Contains(typeof(ISetPropertyCommand<,>)) => CommandRole.SetProperty,
            _ when effects.Contains(typeof(IMoveCommand<,>)) => CommandRole.Move,
            _ => null,
        };
    }

    private static (TextDefinition? Label, string? Icon) DefaultsOf(CommandRole role) => role switch
    {
        CommandRole.Create => (CommonTexts.Create, Icons.Material.Filled.Add),
        CommandRole.Update => (CommonTexts.Edit, Icons.Material.Outlined.Edit),
        CommandRole.Delete => (CommonTexts.Delete, Icons.Material.Outlined.Delete),
        _ => (null, null),
    };

    private static SerginActionKind KindOf(CommandRole role) => role switch
    {
        CommandRole.Create => SerginActionKind.Primary,
        CommandRole.Delete => SerginActionKind.Danger,
        _ => SerginActionKind.Secondary,
    };

    // ---- Fields ----

    private IReadOnlyList<FieldView> Fields(Type? record, TextCategory? category, FieldSetBuilder? declared, out string? title)
    {
        title = declared?.TitleProperty;

        if (record is null)
        {
            return [];
        }

        PropertyInfo[] properties =
        [
            .. record.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetIndexParameters().Length == 0)
                .OrderBy(property => property.MetadataToken),
        ];

        List<FieldView> fields = [];

        for (int index = 0; index < properties.Length; index++)
        {
            PropertyInfo property = properties[index];
            FieldOverride? named = declared?.Overrides.GetValueOrDefault(property.Name);

            if (LabelOf(record, property.Name, category, named) is not { } label)
            {
                continue;
            }

            fields.Add(new FieldView(
                property.Name,
                label,
                GetterOf(property),
                named?.Href,
                named?.Component,
                named?.Hidden ?? (IsGuid(property.PropertyType) || property.Name == "Id"),
                named?.Order ?? index));

            title ??= property.PropertyType == typeof(string) ? property.Name : null;
        }

        return [.. fields.OrderBy(field => field.Order)];
    }

    /// <summary>The property's field text; a property the configuration names to show must have one.</summary>
    private FieldText? LabelOf(Type record, string property, TextCategory? category, FieldOverride? named)
    {
        FieldText? label = category is null ? null : texts.FindField(category, property);

        if (label is null && named is { Hidden: false })
        {
            throw Refuse(
                $"Field(x => x.{property}) on {record.FullName} names a property with no FieldText in "
                + $"{category?.Prefix ?? "any category (the root declares no Texts(...))"}; declare one, or Hidden() it.");
        }

        return label;
    }

    private static Func<object, object?> GetterOf(PropertyInfo property)
    {
        ParameterExpression record = Expression.Parameter(typeof(object), "record");
        Expression value = Expression.Property(Expression.Convert(record, property.DeclaringType!), property);
        return Expression.Lambda<Func<object, object?>>(Expression.Convert(value, typeof(object)), record).Compile();
    }

    private static bool IsGuid(Type type) => (Nullable.GetUnderlyingType(type) ?? type) == typeof(Guid);

    private static ListFieldSet? ListFieldSetOf(Type listQuery)
    {
        const BindingFlags members = BindingFlags.Public | BindingFlags.Static;

        return listQuery.Assembly.GetTypes()
            .Where(type => type is { IsAbstract: true, IsSealed: true, IsGenericTypeDefinition: false } && type.Namespace == listQuery.Namespace)
            .SelectMany(type => type.GetProperties(members)
                .Where(property => property.PropertyType == typeof(ListFieldSet))
                .Select(property => property.GetValue(null)))
            .OfType<ListFieldSet>()
            .FirstOrDefault();
    }

    // ---- Filters ----

    private IReadOnlyList<ListFilterView> Filters(ListFieldSet? set, IListFilterDeclarations? declared)
    {
        IReadOnlyDictionary<string, Type> components = declared?.FilterComponents ?? new Dictionary<string, Type>();

        if (components.Keys.FirstOrDefault(id => set?.Find(id) is not { Kind: ListFieldKind.Identifier }) is { } unknown)
        {
            throw Refuse($"Filter(\"{unknown}\", ...) names no Identifier field of {root.Name}'s list field set.");
        }

        if (set is null)
        {
            return [];
        }

        return
        [
            .. set.Fields
                .Where(field => field.Kind == ListFieldKind.Identifier && field.FilterModes.Count > 0)
                .Select(field => FilterOf(set, field, components.GetValueOrDefault(field.Id))),
        ];
    }

    private ListFilterView FilterOf(ListFieldSet set, ListField field, Type? component)
    {
        if (component is not null)
        {
            return new ListFilterView(field, component, null, null, null);
        }

        if (targets.RootByName(field.Id) is { } target && targets.IsListed(target))
        {
            return new ListFilterView(field, null, target, null, null);
        }

        if (targets.ChildByName(field.Id) is { } child
            && targets.IsListed(child.Root, child.Child)
            && set.Find(AggregateTargets.FilterIdOf(child.Root)) is { Kind: ListFieldKind.Identifier } parent)
        {
            return new ListFilterView(field, null, child.Root, child.Child, parent.Id);
        }

        throw Refuse(
            $"the {root.Name} list's Identifier filter '{field.Id}' names no root or child entity (<name>Id names one called "
            + "<Name>, and it needs an IListOf<Name> query for its options; a child also needs an IChildListOf query, its root's list and the filter named after its root). Give it a picker with List(list => list.Filter(...)).");
    }

    // ---- Forms ----

    private IReadOnlyList<FormFieldView> FormFields(Type? command, Type? getOneResponse)
    {
        if (command is null)
        {
            return [];
        }

        FormViewBuilder? declared = builder.Forms.GetValueOrDefault(command);
        IReadOnlySet<string> declaredByType = CommandTypes.InterfaceMemberNames(command);
        IReadOnlySet<string> hidden = commands.HiddenProperties(command);

        ParameterInfo[] shown =
        [
            .. command.GetConstructors()
                .OrderByDescending(constructor => constructor.GetParameters().Length)
                .First()
                .GetParameters()
                .Where(parameter => !declaredByType.Contains(parameter.Name!) && !hidden.Contains(parameter.Name!)),
        ];

        IEnumerable<string> pickerNames = declared?.ChildPickers.Keys ?? Enumerable.Empty<string>();

        if (pickerNames.FirstOrDefault(name => shown.All(parameter => parameter.Name != name)) is { } unknown)
        {
            throw Refuse($"ChildPicker(x => x.{unknown}) on {command.FullName} names no shown field of its form.");
        }

        return [.. shown.Select(parameter => FormFieldOf(command, parameter, declared?.ChildPickers.GetValueOrDefault(parameter.Name!), getOneResponse))];
    }

    private FormFieldView FormFieldOf(Type command, ParameterInfo parameter, ChildPickerDeclaration? picker, Type? getOneResponse)
    {
        string name = parameter.Name!;
        TextCategory? category = commands.CategoryOf(command);
        FieldText label = (category is null ? null : texts.FindField(category, name)) ?? throw Refuse(
            $"{command.FullName}.{name} is a form field with no FieldText in {category?.Prefix ?? "any category"}.");

        if (picker is not null)
        {
            return ChildPickerOf(command, parameter, label, picker, getOneResponse);
        }

        Type primitive = ValueObjects.PrimitiveOf(parameter.ParameterType) ?? throw Refuse(
            $"{command.FullName}.{name} is of {parameter.ParameterType.FullName}, which wraps no single value a form can edit.");

        FormInputKind? kind = primitive switch
        {
            _ when primitive == typeof(string) => FormInputKind.Text,
            _ when primitive == typeof(bool) => FormInputKind.Switch,
            _ when ValueObjects.IsNumber(primitive) => FormInputKind.Number,
            _ when ValueObjects.IsDate(primitive) => FormInputKind.Date,
            _ when primitive.IsEnum => FormInputKind.Select,
            _ => null,
        };

        if (kind is { } simple)
        {
            return new FormFieldView(name, label, parameter.ParameterType, simple, null, null, null);
        }

        if (primitive != typeof(Guid))
        {
            throw Refuse($"{command.FullName}.{name} is of {primitive.FullName}, which no generated input edits.");
        }

        Type pickerRoot = PickerRootOf(command, parameter);

        if (!targets.IsListed(pickerRoot))
        {
            throw Refuse(
                $"{command.FullName}.{name} is the id of {pickerRoot.Name}, which has no IListOf<{pickerRoot.Name}> query to supply "
                + "the picker's options.");
        }

        return new FormFieldView(name, label, parameter.ParameterType, FormInputKind.Picker, pickerRoot, null, null);
    }

    /// <summary>
    /// The root a typed id field offers: the root whose id type it is, else the root its name names. A child entity's
    /// id, by type or name, needs a <c>ChildPicker(...)</c>.
    /// </summary>
    private Type PickerRootOf(Type command, ParameterInfo parameter)
    {
        Type type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
        string name = parameter.Name!;

        if ((type == typeof(Guid) ? null : targets.RootByIdType(type)) is { } byType)
        {
            return byType;
        }

        if (targets.ChildByIdType(type) is { } childByType)
        {
            throw ChildNeedsPicker(command, name, childByType);
        }

        if (targets.RootByName(name) is { } byName)
        {
            return byName;
        }

        if (targets.ChildByName(name) is { } childByName)
        {
            throw ChildNeedsPicker(command, name, childByName);
        }

        throw Refuse(
            $"{command.FullName}.{name} is a typed id that names no root or child entity: its type is no root's id type, and "
            + "no root or child is called after the field (<Name>Id names <Name>).");
    }

    private InvalidOperationException ChildNeedsPicker(Type command, string name, (Type Child, Type Root) child) =>
        Refuse(
            $"{command.FullName}.{name} is the id of the child entity {child.Child.Name} of {child.Root.Name}. Declare its picker with "
            + $"Form<{command.Name}>(form => form.ChildPicker<{child.Root.Name}, TResponse>(x => x.{name}, record => ...)).");

    private FormFieldView ChildPickerOf(
        Type command, ParameterInfo parameter, FieldText label, ChildPickerDeclaration picker, Type? getOneResponse)
    {
        Type type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
        string name = parameter.Name!;

        (Type Child, Type Root) child = targets.ChildByIdType(type) ?? targets.ChildByName(name) ?? throw Refuse(
            $"ChildPicker(x => x.{name}) on {command.FullName} names a field that is no child entity's id.");

        if (child.Root != picker.ParentRoot)
        {
            throw Refuse(
                $"ChildPicker<{picker.ParentRoot.Name}, …>(x => x.{name}) on {command.FullName}: {child.Child.Name} is a child of "
                + $"{child.Root.Name}, not of {picker.ParentRoot.Name}.");
        }

        if (!targets.IsListed(child.Root, child.Child))
        {
            throw Refuse(
                $"ChildPicker<{picker.ParentRoot.Name}, …>(x => x.{name}) on {command.FullName}: the picker needs an "
                + $"IListOf<{child.Root.Name}> query for the parent and an IChildListOf<{child.Root.Name}, …, {child.Child.Name}> query for its options.");
        }

        if (getOneResponse is not null && picker.Response != getOneResponse)
        {
            throw Refuse(
                $"ChildPicker<…, {picker.Response.Name}>(x => x.{name}) on {command.FullName} reads the parent from "
                + $"{picker.Response.FullName}, which is not {root.Name}'s GetOne response {getOneResponse.FullName}.");
        }

        return new FormFieldView(name, label, parameter.ParameterType, FormInputKind.ChildPicker, child.Child, child.Root, picker.ParentFromRecord);
    }

    // ---- Request shapes ----

    internal static bool Implements(Type request, Type openInterface) =>
        request.GetInterfaces().Any(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == openInterface);

    private static bool IsCommand(Type request) => Implements(request, typeof(ICommand<>));

    private static Type? ResponseOf(Type query) => GenericArgumentOf(query, typeof(IQuery<>));

    private static Type? ListItemOf(Type query) => GenericArgumentOf(query, typeof(IListQuery<>));

    private static Type? GenericArgumentOf(Type request, Type openInterface) =>
        request.GetInterfaces()
            .FirstOrDefault(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == openInterface)
            ?.GetGenericArguments()[0];
}
