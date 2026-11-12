using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Application.Commands.Queries;

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
/// <param name="GetOne">The <c>IGetOneQuery</c>, or null.</param>
/// <param name="List">The <c>IListOf</c> query, or null.</param>
/// <param name="Overview">The <c>IOverviewQuery</c>, or null.</param>
/// <param name="Audit">The <c>IAuditQuery</c>, or null.</param>
/// <param name="Create">The <c>ICreateCommand</c>, or null.</param>
/// <param name="Update">The <c>IUpdateCommand</c> on the root, or null.</param>
/// <param name="Commands">Every command on the root that is an action, by role then name.</param>
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
/// <param name="GetOneInvoker">Sends the GetOne query; null with no GetOne.</param>
/// <param name="BuildGetOne">Builds the GetOne query from the record's id.</param>
/// <param name="ListInvoker">Sends the list query; null with no list.</param>
/// <param name="BuildList">Builds the list query from a page's paging, term, filters and sort.</param>
/// <param name="OverviewInvoker">Sends the overview query; null with no overview.</param>
/// <param name="BuildOverview">Builds the overview query (it targets the whole collection and takes no arguments); null with no overview.</param>
/// <param name="AuditInvoker">Sends the audit query; null with no audit query.</param>
/// <param name="BuildAudit">Builds the audit query from the record's id, or null when it is not id-only.</param>
/// <param name="CreateInvoker">Sends the create command; null with none.</param>
/// <param name="BuildCreate">Builds the create command from form values.</param>
/// <param name="UpdateInvoker">Sends the update command; null with none.</param>
/// <param name="BuildUpdate">Builds the update command from the record's id and form values.</param>
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
public sealed record AggregateViewModel(
    Type Root,
    Type Configuration,
    string Schema,
    TextDefinition Title,
    TextDefinition Singular,
    AggregateViewRoutes Routes,
    AggregateFeatures Features,
    Type? GetOne,
    Type? List,
    Type? Overview,
    Type? Audit,
    Type? Create,
    Type? Update,
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
    RequestInvoker? GetOneInvoker,
    Func<Guid, object>? BuildGetOne,
    RequestInvoker? ListInvoker,
    Func<ListQueryInput, object>? BuildList,
    RequestInvoker? OverviewInvoker,
    Func<object>? BuildOverview,
    RequestInvoker? AuditInvoker,
    Func<Guid, object>? BuildAudit,
    RequestInvoker? CreateInvoker,
    Func<Guid, IReadOnlyDictionary<string, object?>, object>? BuildCreate,
    RequestInvoker? UpdateInvoker,
    Func<Guid, IReadOnlyDictionary<string, object?>, object>? BuildUpdate,
    Func<object, IReadOnlyDictionary<string, object?>>? DetailArguments,
    TextDefinition? ListSearchPlaceholder = null,
    IReadOnlyDictionary<string, FieldText>? ListFieldLabels = null,
    Func<object, IReadOnlyDictionary<string, object?>>? ListItemArguments = null,
    Type? ListItem = null,
    Type? ListBodyType = null,
    TextDefinition? CreateTitle = null,
    TextDefinition? UpdateTitle = null);
