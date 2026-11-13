using System.Reflection;
using MudBlazor;
using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Configuration;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;
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
        CommandView[] commandViews = [.. Commands(requests, builder, singular, child: false, [getOneResponse, listItem])];

        Type? overview = Single(requests, typeof(IOverviewQuery<>));
        Type? entityOverview = Single(requests, typeof(IEntityOverviewQuery<,>));
        Type? audit = Single(requests, typeof(IAuditQuery<,>));

        AggregateViewGuard.EnsureConfigured(view, commandViews, getOne, getOneResponse, list, listItem, create, update, overview, entityOverview, audit);

        IReadOnlyList<FieldView> detailFields = Fields(getOneResponse, CategoryOf(getOne), builder.DetailFields, out string? detailTitle);
        IReadOnlyList<FieldView> listFields = Fields(listItem, CategoryOf(list), builder.ListFields, out string? listTitle);
        ListFieldSet? listFieldSet = list is null ? null : ListFieldSetOf(list);
        IReadOnlyList<ListFilterView> filters = Filters(listFieldSet, builder.ListFields as IListFilterDeclarations);

        return new AggregateViewModel(
            root,
            view.Configuration,
            view.Schema,
            builder.ListTitle ?? singular,
            singular,
            builder.DeclaredRoutes,
            aggregates.For(root),
            Request(getOne, RequestFactory.ForId),
            Request(list, RequestFactory.ForList),
            Request(overview, RequestFactory.ForNone),
            Request(entityOverview, RequestFactory.ForId),
            Request(audit, RequestFactory.ForId),
            Request(create, RequestFactory.ForForm),
            Request(update, RequestFactory.ForForm),
            commandViews,
            detailFields,
            listFields,
            listFieldSet,
            FormFields(create, builder, getOneResponse),
            FormFields(update, builder, getOneResponse),
            new Dictionary<SerginViewSlot, Type>(builder.Slots),
            commandViews.Any(command => command.ScheduleName is not null),
            detailTitle,
            listTitle,
            filters,
            Children(requests, getOneResponse, listItem),
            getOneResponse is null ? null : ArgumentsReader(getOneResponse),
            (builder.ListFields as IListFilterDeclarations)?.SearchPlaceholder,
            ListFieldLabels(listFieldSet, list),
            listItem is null ? null : ArgumentsReader(listItem),
            listItem,
            listItem is null ? null : typeof(GeneratedListBody<>).MakeGenericType(listItem),
            create is null ? null : builder.Forms.GetValueOrDefault(create)?.DeclaredTitle,
            update is null ? null : builder.Forms.GetValueOrDefault(update)?.DeclaredTitle,
            builder.DetailOverviewIsCollapsed);
    }

    // ---- Children ----

    /// <summary>One view model per child entity the root's typed requests name, by type name; a declared child no request names is refused.</summary>
    private IReadOnlyList<ChildViewModel> Children(Type[] requests, Type? getOneResponse, Type? listItem)
    {
        Dictionary<Type, List<Type>> byChild = [];

        foreach (Type request in requests)
        {
            foreach (Type child in AggregateTargets.ChildrenNamedBy(request, CommandTypes.TargetOf(request)!).Distinct())
            {
                if (!byChild.TryGetValue(child, out List<Type>? named))
                {
                    byChild[child] = named = [];
                }

                named.Add(request);
            }
        }

        if (builder.Children.Keys.FirstOrDefault(child => !byChild.ContainsKey(child)) is { } unknown)
        {
            throw Refuse(
                $"Child<{unknown.Name}>() names {unknown.FullName}, which is no child entity of {root.Name}: "
                + $"none of {root.Name}'s typed requests names it as its child.");
        }

        ChildViewModelJoin join = new(this, getOneResponse, listItem);

        return [.. byChild.Keys.OrderBy(child => child.FullName, StringComparer.Ordinal).Select(child => join.Build(child, [.. byChild[child]]))];
    }

    // Every public property's compiled, unwrapping reader, made once; a read fills a fresh bag.
    internal static Func<object, IReadOnlyDictionary<string, object?>> ArgumentsReader(Type record)
    {
        (string Name, Func<object, object?> Get)[] readers =
        [
            .. record.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetIndexParameters().Length == 0)
                .Select(property => (property.Name, ValueObjects.UnwrappedGetter(property))),
        ];

        return value =>
        {
            Dictionary<string, object?> arguments = new(readers.Length, StringComparer.Ordinal);

            foreach ((string name, Func<object, object?> get) in readers)
            {
                arguments[name] = get(value);
            }

            return arguments;
        };
    }

    internal static ViewRequest<TBuild>? Request<TBuild>(Type? request, Func<Type, TBuild> build)
        where TBuild : Delegate =>
        request is null ? null : new ViewRequest<TBuild>(request, RequestInvoker.For(request), build(request));

    internal DeclaredView View => view;

    internal InvalidOperationException Refuse(string problem) =>
        new($"Aggregate view configuration {view.Configuration.FullName}: {problem}");

    internal Type? Single(Type[] requests, Type openInterface)
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

    internal TextCategory? CategoryOf(Type? request) =>
        (request is null ? null : commands.CategoryOf(request)) ?? aggregates.TextsOf(root);

    // ---- Commands ----

    /// <summary>
    /// The actions among <paramref name="requests"/>: the commands whose role is the root's, or a child's when
    /// <paramref name="child"/>. <paramref name="records"/> are the records a <c>DisabledWhen</c> may read.
    /// </summary>
    internal IEnumerable<CommandView> Commands(
        IEnumerable<Type> requests, ViewDeclarationBuilder declarations, TextDefinition singular, bool child, IReadOnlyCollection<Type?> records)
    {
        foreach (Type request in requests.Where(type => !CommandTypes.IsSystem(type) && IsCommand(type)))
        {
            if (RoleOf(request, child) is not { } role)
            {
                continue;
            }

            // Refused here, naming the configuration, before RequestFactory would refuse it without.
            _ = ConstructorOf(request);
            declarations.Commands.TryGetValue(request, out CommandViewBuilder? declared);
            SerginEditMode mode = declared?.DeclaredEditMode ?? SerginEditMode.Dialog;
            AggregateViewGuard.EnsureEditable(view, request, role, child, mode);

            if (declared?.DisabledRecord is { } disabledRecord)
            {
                AggregateViewGuard.EnsureDisabledRecord(view, request, disabledRecord, records);
            }

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
                label ?? singular,
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
                !child && RequestFactory.IsIdOnly(request) ? RequestFactory.ForId(request) : null,
                declared?.DeclaredBulkSucceededText,
                declared?.ConfirmManyTitle,
                declared?.ConfirmManyText,
                mode,
                declared?.DeclaredDisabledWhen,
                declared?.DeclaredDisabledReason,
                child && RequestFactory.IsChildIdOnly(request) ? RequestFactory.ForChildId(request) : null);
        }
    }

    /// <summary>The role of a command on the root itself or, when <paramref name="child"/>, on a child entity; null for another effect.</summary>
    private static CommandRole? RoleOf(Type request, bool child)
    {
        IReadOnlyList<Type> effects = CommandTypes.EffectsOf(request);

        return child
            ? effects switch
            {
                _ when effects.Contains(typeof(IAddChildCommand<,,,>)) => CommandRole.AddChild,
                _ when effects.Contains(typeof(IRemoveChildCommand<,,,>)) => CommandRole.RemoveChild,
                _ when effects.Contains(typeof(IUpdateCommand<,,,>)) => CommandRole.Update,
                _ when effects.Contains(typeof(ITransitionCommand<,,,>)) => CommandRole.Transition,
                _ when effects.Contains(typeof(ISetPropertyCommand<,,,>)) => CommandRole.SetProperty,
                _ => null,
            }
            : effects switch
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
        CommandRole.AddChild => (CommonTexts.Add, Icons.Material.Filled.Add),
        CommandRole.RemoveChild => (CommonTexts.Remove, Icons.Material.Outlined.Delete),
        _ => (null, null),
    };

    private static SerginActionKind KindOf(CommandRole role) => role switch
    {
        CommandRole.Create or CommandRole.AddChild => SerginActionKind.Primary,
        CommandRole.Delete or CommandRole.RemoveChild => SerginActionKind.Danger,
        _ => SerginActionKind.Secondary,
    };

    // ---- Fields ----

    internal IReadOnlyList<FieldView> Fields(Type? record, TextCategory? category, FieldSetBuilder? declared, out string? title)
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
                ValueObjects.UnwrappedGetter(property),
                named?.Href,
                named?.Component,
                named?.Hidden ?? (IsGuid(property.PropertyType) || property.Name == "Id"),
                named?.Order ?? index,
                named?.Format,
                named?.FormatPlaceholder ?? "Value"));

            // A string, or a value object wrapping one: the record's name as stored.
            title ??= ValueObjects.PrimitiveOf(property.PropertyType) == typeof(string) ? property.Name : null;
        }

        return [.. fields.OrderBy(field => field.Order)];
    }

    /// <summary>The property's label: the one the configuration declared, else its field text's; a property the configuration names to show must have one.</summary>
    private TextDefinition? LabelOf(Type record, string property, TextCategory? category, FieldOverride? named)
    {
        TextDefinition? label = named?.Label ?? (category is null ? null : texts.FindField(category, property))?.Label;

        if (label is null && named is { Hidden: false })
        {
            throw Refuse(
                $"Field(x => x.{property}) on {record.FullName} names a property with no FieldText in "
                + $"{category?.Prefix ?? "any category (the root declares no Texts(...))"}; declare one, give it Label(...), or Hidden() it.");
        }

        return label;
    }

    private static bool IsGuid(Type type) => (Nullable.GetUnderlyingType(type) ?? type) == typeof(Guid);

    internal static ListFieldSet? ListFieldSetOf(Type listQuery)
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
            return new ListFilterView(field, null, target, null, null, OptionsOf(field, () => PickerOptions.ForRoot(ListQueryOf(target))));
        }

        if (targets.ChildByName(field.Id) is { } child
            && targets.IsListed(child.Root, child.Child)
            && set.Find(AggregateTargets.FilterIdOf(child.Root)) is { Kind: ListFieldKind.Identifier } parent)
        {
            (Type childList, Type rootId) = ChildListQueryOf(child.Root, child.Child);

            return new ListFilterView(
                field, null, child.Root, child.Child, parent.Id, OptionsOf(field, () => PickerOptions.ForChild(childList, rootId)));
        }

        throw Refuse(
            $"the {root.Name} list's Identifier filter '{field.Id}' names no root or child entity (<name>Id names one called "
            + "<Name>, and it needs an IListOf<Name> query for its options; a child also needs an IChildListOf query, its root's list and the filter named after its root). Give it a picker with List(list => list.Filter(...)).");
    }

    // A picker whose list query a view cannot build or read is refused at start, naming the filter.
    private PickerOptions OptionsOf(ListField field, Func<PickerOptions> make)
    {
        try
        {
            return make();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"Aggregate view configuration {view.Configuration.FullName}: the {root.Name} list's filter '{field.Id}' cannot load its options. "
                + $"{exception.Message} Give it a picker with List(list => list.Filter(...)).",
                exception);
        }
    }

    // The target is listed (AggregateTargets.IsListed), so its list query is among the request types.
    private Type ListQueryOf(Type target) =>
        commands.RequestTypes
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .First(request => typeof(IListOf<>).MakeGenericType(target).IsAssignableFrom(request));

    private (Type Query, Type RootId) ChildListQueryOf(Type childRoot, Type child) =>
        commands.RequestTypes
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .SelectMany(request => request.GetInterfaces()
                .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IChildListOf<,,>))
                .Select(contract => (Request: request, Arguments: contract.GetGenericArguments())))
            .Where(candidate => candidate.Arguments[0] == childRoot && candidate.Arguments[2] == child)
            .Select(candidate => (candidate.Request, candidate.Arguments[1]))
            .First();

    // Each list field's field text in the list query's category, matched as FieldTextGuard matches it (ignoring case).
    private Dictionary<string, FieldText> ListFieldLabels(ListFieldSet? set, Type? list)
    {
        Dictionary<string, FieldText> labels = new(StringComparer.Ordinal);

        if (set is null || CategoryOf(list) is not { } category)
        {
            return labels;
        }

        IReadOnlyList<FieldText> fields = texts.FieldsOf(category);

        foreach (ListField field in set.Fields)
        {
            if (fields.FirstOrDefault(text => string.Equals(text.PropertyName, field.Id, StringComparison.OrdinalIgnoreCase)) is { } label)
            {
                labels[field.Id] = label;
            }
        }

        return labels;
    }

    // ---- Forms ----

    internal IReadOnlyList<FormFieldView> FormFields(Type? command, ViewDeclarationBuilder declarations, Type? getOneResponse)
    {
        if (command is null)
        {
            return [];
        }

        FormViewBuilder? declared = declarations.Forms.GetValueOrDefault(command);
        IReadOnlySet<string> declaredByType = CommandTypes.InterfaceMemberNames(command);
        IReadOnlySet<string> hidden = commands.HiddenProperties(command);

        ParameterInfo[] shown =
        [
            .. ConstructorOf(command)
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
            IReadOnlyList<object>? values = simple == FormInputKind.Select ? [.. Enum.GetValues(primitive).Cast<object>()] : null;

            return new FormFieldView(name, label, parameter.ParameterType, simple, null, null, null, primitive, EnumValues: values);
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

        return new FormFieldView(
            name,
            label,
            parameter.ParameterType,
            FormInputKind.Picker,
            pickerRoot,
            null,
            null,
            primitive,
            FormOptionsOf(command, name, () => PickerOptions.ForRoot(ListQueryOf(pickerRoot))));
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

        (Type childList, Type rootId) = ChildListQueryOf(child.Root, child.Child);

        return new FormFieldView(
            name,
            label,
            parameter.ParameterType,
            FormInputKind.ChildPicker,
            child.Child,
            child.Root,
            picker.ParentFromRecord,
            typeof(Guid),
            FormOptionsOf(command, name, () => PickerOptions.ForChild(childList, rootId)),
            FormOptionsOf(command, name, () => PickerOptions.ForRoot(ListQueryOf(child.Root))),
            ParentLabelOf(command, name, child.Root),
            picker.ParentFirst ?? ShellTexts.Forms.SelectParentFirst);
    }

    // The parent select's label: the <Parent>Id field text of the command's category, else the parent root's name.
    private FormParentLabel ParentLabelOf(Type command, string name, Type parentRoot)
    {
        TextCategory? category = commands.CategoryOf(command);

        if ((category is null ? null : texts.FindField(category, $"{parentRoot.Name}Id")) is { } field)
        {
            return new FormParentLabel(field.Label, field.Placeholder);
        }

        TextDefinition parentName = aggregates.NameOf(parentRoot) ?? throw Refuse(
            $"ChildPicker(x => x.{name}) on {command.FullName}: its parent select has no label. Declare a {parentRoot.Name}Id "
            + $"FieldText in {category?.Prefix ?? "the command's category"}, or Named(...) on {parentRoot.Name}'s aggregate feature configuration.");

        return new FormParentLabel(parentName, null);
    }

    // A picker whose list query a form cannot build or read is refused at start, naming the field.
    private PickerOptions FormOptionsOf(Type command, string name, Func<PickerOptions> make)
    {
        try
        {
            return make();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"Aggregate view configuration {view.Configuration.FullName}: the form field {command.FullName}.{name} cannot load its options. "
                + exception.Message,
                exception);
        }
    }

    // ---- Request shapes ----

    /// <summary>The one public constructor a view builds <paramref name="request"/> with; refused, naming it, when it has none or several.</summary>
    internal ConstructorInfo ConstructorOf(Type request)
    {
        ConstructorInfo[] constructors = request.GetConstructors();

        return constructors.Length == 1
            ? constructors[0]
            : throw Refuse(
                $"{request.FullName} has {constructors.Length} public constructors; a generated view builds a command with its one "
                + "public constructor, so give it exactly one.");
    }

    internal static bool Implements(Type request, Type openInterface) =>
        request.GetInterfaces().Any(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == openInterface);

    internal static bool IsCommand(Type request) => Implements(request, typeof(ICommand<>));

    internal static Type? ResponseOf(Type query) => GenericArgumentOf(query, typeof(IQuery<>));

    /// <summary>The <c>TItem</c> of the <c>IListQuery&lt;TItem&gt;</c> <paramref name="query"/> implements, or null.</summary>
    internal static Type? ListItemOf(Type query) => GenericArgumentOf(query, typeof(IListQuery<>));

    private static Type? GenericArgumentOf(Type request, Type openInterface) =>
        request.GetInterfaces()
            .FirstOrDefault(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == openInterface)
            ?.GetGenericArguments()[0];
}
