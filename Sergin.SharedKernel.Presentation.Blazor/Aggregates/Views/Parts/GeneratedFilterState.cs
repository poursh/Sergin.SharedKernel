using System.Globalization;
using MudBlazor;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Pages;
using Sergin.SharedKernel.Presentation.Blazor.Times;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

/// <summary>One field of a generated filter panel: an identifier picker (generated or configured), or a date range.</summary>
/// <param name="Field">The list field.</param>
/// <param name="Label">Its field text: the picker's label and the chip's field name.</param>
/// <param name="Identifier">The identifier filter, or null for a date range.</param>
public sealed record GeneratedFilterEntry(ListField Field, FieldText Label, ListFilterView? Identifier);

/// <summary>The services a generated filter panel reads its options and formats its chips with.</summary>
public sealed record GeneratedFilterServices(
    ISerginDispatcher Dispatcher,
    IUiErrorPresenter ErrorPresenter,
    IStringLocalizer Localizer,
    ICultureFormatter Formatter,
    IUiTimeZoneStore TimeZones);

/// <summary>
/// What a generated list's filter panel holds, kept by the list body so it outlives the panel (which renders only while
/// open): each field's chosen value, the pickers' options (loaded the first time the panel opens; a child's when its
/// root is chosen), and the configured filter components, which may name their value for the chip.
/// </summary>
/// <remarks>
/// The fields come from the list query's <see cref="ListFieldSet"/>, in its order: an <c>Identifier</c> field with
/// <c>equals</c> through the model's <see cref="ListFilterView"/>, an <c>Instant</c> field with <c>between</c> as whole
/// days in the viewer's zone. A child picker stays disabled until its root's picker has a value, and loses its value
/// when the root changes.
/// </remarks>
public sealed class GeneratedFilterState
{
    private readonly GeneratedFilterServices services;
    private readonly Dictionary<string, Guid?> values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<PickerOption>> options = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateRange?> ranges = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object> components = new(StringComparer.Ordinal);
    private TimeZoneInfo timeZone = TimeZoneInfo.Utc;
    private bool loaded;

    public GeneratedFilterState(AggregateViewModel model, GeneratedFilterServices services)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(services);

        this.services = services;
        Entries = [.. EntriesOf(model)];
    }

    public IReadOnlyList<GeneratedFilterEntry> Entries { get; }

    public bool IsEmpty => Entries.Count == 0;

    public Guid? ValueOf(string id) => values.GetValueOrDefault(id);

    public IReadOnlyList<PickerOption> OptionsOf(string id) => options.GetValueOrDefault(id) ?? [];

    public DateRange? RangeOf(string id) => ranges.GetValueOrDefault(id);

    public void SetRange(string id, DateRange? range) => ranges[id] = range;

    public bool IsDisabled(ListFilterView filter) => filter.ParentFilterId is { } parent && ValueOf(parent) is null;

    /// <summary>Keeps a configured filter component's instance, so the chip can ask it to name its value.</summary>
    public void Capture(string id, object component) => components[id] = component;

    /// <summary>The root pickers' options and the viewer's zone, read once, the first time the panel opens.</summary>
    public async Task LoadAsync()
    {
        if (loaded)
        {
            return;
        }

        foreach (ListFilterView filter in Identifiers())
        {
            if (filter.Options is { IsChild: false } source)
            {
                await LoadOptionsAsync(filter.Field.Id, source, Guid.Empty);
            }
        }

        timeZone = await services.TimeZones.GetTimeZoneAsync();
        loaded = true;
    }

    /// <summary>Sets a picker's value; the pickers under it lose theirs and, given a value, load its children.</summary>
    public async Task SetValueAsync(string id, Guid? value)
    {
        values[id] = value;

        foreach (ListFilterView child in ChildrenOf(id))
        {
            Clear(child.Field.Id);
            options.Remove(child.Field.Id);

            if (value is { } parentId && child.Options is { } source)
            {
                await LoadOptionsAsync(child.Field.Id, source, parentId);
            }
        }
    }

    /// <summary>Clears one field, and the pickers under it (their options too, since they were the old root's).</summary>
    public void Clear(string id)
    {
        values.Remove(id);
        ranges.Remove(id);

        foreach (ListFilterView child in ChildrenOf(id))
        {
            options.Remove(child.Field.Id);
            Clear(child.Field.Id);
        }
    }

    public void ClearAll()
    {
        foreach (GeneratedFilterEntry entry in Entries)
        {
            Clear(entry.Field.Id);
        }
    }

    /// <summary>The filters the chosen values make, each with its chip: the field's label, then what was chosen.</summary>
    public IReadOnlyList<SerginAppliedFilter> Build()
    {
        List<SerginAppliedFilter> filters = [];

        foreach (GeneratedFilterEntry entry in Entries)
        {
            string id = entry.Field.Id;

            if (entry.Identifier is { } identifier)
            {
                if (ValueOf(id) is { } value)
                {
                    filters.Add(new(id, FilteringType.equals, value.ToString(), Chip(entry, LabelOf(identifier, value))));
                }
            }
            else if (RangeOf(id) is { } range && (range.Start is not null || range.End is not null))
            {
                // Whole days in the viewer's zone: from the start of the first day to the start of the day after the last.
                string? from = range.Start is { } start ? ToUtcIso(start.Date) : null;
                string? to = range.End is { } end ? ToUtcIso(end.Date.AddDays(1)) : null;
                string days = services.Localizer.Text(ShellTexts.Lists.DateRange, new { From = DayLabel(range.Start), To = DayLabel(range.End) });

                filters.Add(new(id, FilteringType.between, new[] { from, to }, Chip(entry, days)));
            }
        }

        return [.. filters];
    }

    private static IEnumerable<GeneratedFilterEntry> EntriesOf(AggregateViewModel model)
    {
        if (model.ListFieldSet is not { } set || model.ListFieldLabels is not { } labels)
        {
            yield break;
        }

        foreach (ListField field in set.Fields)
        {
            if (!labels.TryGetValue(field.Id, out FieldText? label))
            {
                continue;
            }

            if (field.Kind == ListFieldKind.Identifier && field.FilterModes.Contains(FilteringType.equals)
                && model.ListFilters.FirstOrDefault(filter => filter.Field.Id == field.Id) is { } identifier)
            {
                yield return new GeneratedFilterEntry(field, label, identifier);
            }
            else if (field.Kind == ListFieldKind.Instant && field.FilterModes.Contains(FilteringType.between))
            {
                yield return new GeneratedFilterEntry(field, label, Identifier: null);
            }
        }
    }

    private IEnumerable<ListFilterView> Identifiers() => Entries.Select(entry => entry.Identifier).OfType<ListFilterView>();

    private IEnumerable<ListFilterView> ChildrenOf(string id) => Identifiers().Where(filter => filter.ParentFilterId == id);

    private async Task LoadOptionsAsync(string id, PickerOptions source, Guid parentId)
    {
        ErrorOr<IReadOnlyList<PickerOption>> result = await source.LoadAsync(services.Dispatcher, parentId);

        if (result.IsError)
        {
            services.ErrorPresenter.Notify(result.FirstError);
            return;
        }

        options[id] = result.Value;
    }

    private string LabelOf(ListFilterView filter, Guid value) =>
        filter.Component is not null
            ? (components.GetValueOrDefault(filter.Field.Id) as IListFilterLabel)?.LabelOf(value) ?? value.ToString()
            : OptionsOf(filter.Field.Id).FirstOrDefault(option => option.Id == value)?.Label ?? "?";

    private string Chip(GeneratedFilterEntry entry, string value) =>
        services.Localizer.Text(ShellTexts.Lists.FilterChip, new { Field = services.Localizer.Text(entry.Label.Label), Value = value });

    private string ToUtcIso(DateTime localDay) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDay, DateTimeKind.Unspecified), timeZone).ToString("O", CultureInfo.InvariantCulture);

    // The picked day is a local calendar day, so it is shown as it is, without a zone shift.
    private string DayLabel(DateTime? day) => day is { } value ? services.Formatter.Date(new DateTimeOffset(value.Date, TimeSpan.Zero)) : "…";
}
