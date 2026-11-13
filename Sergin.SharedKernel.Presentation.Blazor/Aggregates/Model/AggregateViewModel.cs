using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Application.Commands.Queries;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// Everything a generated view needs about one configured root, joined once at start from its typed requests, their
/// configurations, the root's features and texts, and its view configuration. Nothing is reflected per render.
/// </summary>
/// <param name="Root">The aggregate root type.</param>
/// <param name="Configuration">The view configuration type that declared it.</param>
/// <param name="Schema">The schema of the module whose UI assembly holds the configuration.</param>
/// <param name="Title">The list page's title: the configured <c>Title(...)</c>, else <paramref name="Singular"/>.</param>
/// <param name="Singular">The root's name, from its feature configuration's <c>Named(...)</c>: detail and form titles.</param>
/// <param name="Routes">Where its pages live.</param>
/// <param name="Features">The root's aggregate features.</param>
/// <param name="GetOne">The <c>IGetOneQuery</c>, its invoker and its build from the record's id, or null.</param>
/// <param name="List">The <c>IListOf</c> query, its invoker and its build from a page's paging, term, filters and sort, or null.</param>
/// <param name="Overview">The <c>IOverviewQuery</c>, its invoker and its build (it targets the whole collection and takes no arguments), or null.</param>
/// <param name="EntityOverview">The <c>IEntityOverviewQuery</c>, its invoker and its build from the record's id, or null.</param>
/// <param name="Audit">The <c>IAuditQuery</c> with its invoker, built from the record's id, or null.</param>
/// <param name="Create">The <c>ICreateCommand</c>, its invoker and its build from form values, or null.</param>
/// <param name="Update">The <c>IUpdateCommand</c> on the root, its invoker and its build from the record's id and form values, or null.</param>
/// <param name="Commands">Every command on the root that is an action, in the order of their request types' full names.</param>
/// <param name="DetailFields">The GetOne response's fields, hidden ones included, in order.</param>
/// <param name="ListFields">The list item's fields, hidden ones included, in order.</param>
/// <param name="ListFieldSet">The list query's declared sort and filter fields, or null.</param>
/// <param name="CreateFields">The create command's shown fields.</param>
/// <param name="UpdateFields">The update command's shown fields.</param>
/// <param name="Slots">The components the configuration put in the views' slots.</param>
/// <param name="HasSchedulableCommand">Whether any command is <c>Schedulable(name)</c>.</param>
/// <param name="DetailTitleField">The GetOne response's display property: breadcrumb tail, fields title, schedule target label.</param>
/// <param name="ListTitleField">The list item's display property.</param>
/// <param name="ListFilters">The list's identifier filters and where their options come from.</param>
/// <param name="Children">The root's child entities, by type name: one for each child the root's typed requests name.</param>
/// <param name="DetailArguments">
/// Reads every public property of a GetOne response into a name-to-value bag, value objects read through to their
/// primitive: what fills a text's placeholders from the loaded record. Compiled once; null with no GetOne.
/// </param>
/// <param name="ListSearchPlaceholder">The list's quick-search placeholder, from <c>SearchPlaceholder(...)</c>; null for the shell's.</param>
/// <param name="ListFieldLabels">Each list field's field text in the list query's category, by list field id.</param>
/// <param name="ListItemArguments">
/// Reads a list item into a name-to-value bag the way <paramref name="DetailArguments"/> reads a GetOne response: what
/// fills a row command's texts. Compiled once; null with no list.
/// </param>
/// <param name="ListItem">The list query's item type; null with no list.</param>
/// <param name="ListBodyType">The list view's body closed over <paramref name="ListItem"/>, made at registry build; null with no list.</param>
/// <param name="CreateTitle">The create form's title, from <c>Form&lt;T&gt;(form => form.Title(...))</c>; null for the shell's "New {Name}".</param>
/// <param name="UpdateTitle">The edit form's title, likewise; null for the shell's "Edit {Name}".</param>
/// <param name="DetailOverviewCollapsed">Whether the detail overview starts collapsed, from <c>DetailOverviewCollapsed()</c>.</param>
/// <param name="Links">The links to other pages from <c>Link(...)</c>: detail bar actions and list row icons; null for none.</param>
/// <param name="Parent">
/// The parent root of a root listed under one (<c>ListUnder&lt;TParent&gt;</c>): its own model gives the list's trail and
/// loads the parent record; null for any other root.
/// </param>
public sealed record AggregateViewModel(
    Type Root,
    Type Configuration,
    string Schema,
    TextDefinition Title,
    TextDefinition Singular,
    AggregateViewRoutes Routes,
    AggregateFeatures Features,
    ViewRequest<Func<Guid, object>>? GetOne,
    ViewRequest<Func<ListQueryInput, object>>? List,
    ViewRequest<Func<object>>? Overview,
    ViewRequest<Func<Guid, object>>? EntityOverview,
    ViewRequest<Func<Guid, object>>? Audit,
    ViewRequest<Func<Guid, IReadOnlyDictionary<string, object?>, object>>? Create,
    ViewRequest<Func<Guid, IReadOnlyDictionary<string, object?>, object>>? Update,
    IReadOnlyList<CommandView> Commands,
    IReadOnlyList<FieldView> DetailFields,
    IReadOnlyList<FieldView> ListFields,
    ListFieldSet? ListFieldSet,
    IReadOnlyList<FormFieldView> CreateFields,
    IReadOnlyList<FormFieldView> UpdateFields,
    IReadOnlyDictionary<SerginViewSlot, Type> Slots,
    bool HasSchedulableCommand,
    string? DetailTitleField,
    string? ListTitleField,
    IReadOnlyList<ListFilterView> ListFilters,
    IReadOnlyList<ChildViewModel> Children,
    Func<object, IReadOnlyDictionary<string, object?>>? DetailArguments,
    TextDefinition? ListSearchPlaceholder = null,
    IReadOnlyDictionary<string, FieldText>? ListFieldLabels = null,
    Func<object, IReadOnlyDictionary<string, object?>>? ListItemArguments = null,
    Type? ListItem = null,
    Type? ListBodyType = null,
    TextDefinition? CreateTitle = null,
    TextDefinition? UpdateTitle = null,
    bool DetailOverviewCollapsed = false,
    IReadOnlyList<ViewLinkView>? Links = null,
    Type? Parent = null)
{
    /// <summary>
    /// Loads the record <paramref name="id"/> through the GetOne query: at its version for a <c>Versioned()</c> root, plainly
    /// for any other (which publishes no version, so a versioned send would answer <c>VersionErrors.NotPublished</c>).
    /// </summary>
    internal Task<ViewSendResult> LoadRecordAsync(ISerginDispatcher dispatcher, Guid id, CancellationToken cancellationToken = default)
    {
        ViewRequest<Func<Guid, object>> getOne = GetOne
            ?? throw new InvalidOperationException($"{Root.FullName} has no GetOne query to load a record with.");
        object query = getOne.Build(id);

        return Features.Versioned
            ? getOne.Invoker.SendVersionedAsync(dispatcher, query, expected: null, cancellationToken)
            : getOne.Invoker.SendAsync(dispatcher, query, cancellationToken);
    }

    /// <summary>The create command (no <paramref name="id"/>) or the update command of <paramref name="id"/>, built from a form's values.</summary>
    internal object BuildForm(Guid? id, IReadOnlyDictionary<string, object?> values) =>
        id is { } target
            ? (Update ?? throw new InvalidOperationException($"{Root.FullName} has no update command.")).Build(target, values)
            : (Create ?? throw new InvalidOperationException($"{Root.FullName} has no create command.")).Build(Guid.Empty, values);

    /// <summary>
    /// Sends a form's create or update command, the update at <paramref name="version"/> when it is version-guarded.
    /// </summary>
    internal Task<ViewSendResult> SubmitFormAsync(
        ISerginDispatcher dispatcher, Guid? id, IReadOnlyDictionary<string, object?> values, RowVersion? version, CancellationToken cancellationToken = default)
    {
        object request = BuildForm(id, values);
        RequestInvoker invoker = (id is null ? Create : Update)?.Invoker
            ?? throw new InvalidOperationException($"{Root.FullName} has no {(id is null ? "create" : "update")} command.");
        bool versioned = id is not null && CommandOf(Update!.Type).RequiresVersion;

        return versioned
            ? invoker.SendVersionedAsync(dispatcher, request, version, cancellationToken)
            : invoker.SendAsync(dispatcher, request, cancellationToken);
    }

    /// <summary>The command view of <paramref name="request"/>; every command a form sends has one, so a missing one is a bug, not "unversioned".</summary>
    internal CommandView CommandOf(Type request) =>
        Commands.SingleOrDefault(command => command.Request == request)
        ?? throw new InvalidOperationException($"{Root.FullName} has no command view for {request.FullName}.");
}
