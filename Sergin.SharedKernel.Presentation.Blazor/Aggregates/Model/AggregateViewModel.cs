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
    IReadOnlyList<ListFilterView> ListFilters);
