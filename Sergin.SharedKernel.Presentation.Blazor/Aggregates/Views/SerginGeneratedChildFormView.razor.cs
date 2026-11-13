using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Modules;
using Sergin.SharedKernel.Presentation.Blazor.Navigation;
using Sergin.SharedKernel.Presentation.Blazor.Validation;
using Sergin.SharedKernel.Presentation.Blazor.Views;
using Sergin.SharedKernel.Presentation.Errors;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views;

/// <summary>
/// The add (no <see cref="Id"/>) or edit form of <typeparamref name="TChild"/>, a child entity of <typeparamref name="TRoot"/>,
/// generated from the root's <see cref="AggregateViewModel"/> and the child's <see cref="ChildViewModel"/>: an input per
/// shown field of its add or edit command, validated field by field by the pipeline's own validator over the command
/// built from the root id, the child id and the form's values (<see cref="GeneratedChildForm.Build"/>, the same build the
/// submit sends).
/// </summary>
/// <remarks>
/// The root is read first and keeps its version, since a child command changes the root, which is what is guarded; an
/// unknown root (the add form included) is the problem panel, the trail still linking the root by <see cref="RootId"/>.
/// An edit then reads the child and prefills each field from the property of the same name. The submit goes through
/// <see cref="GeneratedChildForm.SubmitAsync"/> at the root's version: with none it reports not found and reads again, on
/// a stale answer it reads both again. It leaves for the child's detail page, or after an add without one for the root's.
/// </remarks>
public sealed partial class SerginGeneratedChildFormView<TRoot, TChild>
    where TRoot : class, IAggregateRoot
    where TChild : class
{
    private readonly GeneratedFormValues values = new();

    private object? record;
    private object? rootRecord;
    private RowVersion? version;
    private SerginProblem? problem;
    private bool submitting;
    private FieldView? titleField;
    private FieldView? rootTitleField;
    private Func<object, string, Task<IEnumerable<string>>> validation = default!;

    [Parameter]
    public Guid RootId { get; set; }

    /// <summary>The child to edit; null for the add form.</summary>
    [Parameter]
    public Guid? Id { get; set; }

    [Inject]
    private AggregateViewRegistry Views { get; set; } = default!;

    [Inject]
    private SerginUiModuleCatalog Catalog { get; set; } = default!;

    [Inject]
    private ISerginDispatcher Dispatcher { get; set; } = default!;

    [Inject]
    private ISerginFormValidator FormValidator { get; set; } = default!;

    [Inject]
    private IUiErrorPresenter ErrorPresenter { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    [CascadingParameter]
    private SerginViewHost Host { get; set; } = SerginViewHost.Unhosted;

    private AggregateViewModel Model { get; set; } = default!;

    private ChildViewModel Child { get; set; } = default!;

    private bool IsEdit => Id is not null;

    private IReadOnlyList<FormFieldView> Fields => IsEdit ? Child.EditFields : Child.AddFields;

    private RequestInvoker Invoker => IsEdit ? Child.Edit!.Invoker : Child.Add!.Invoker;

    // The form waits for the root (its version) and, editing, the child.
    private bool Ready => rootRecord is not null && (!IsEdit || record is not null);

    private TextDefinition TitleText =>
        (IsEdit ? Child.EditTitle : Child.AddTitle) ?? (IsEdit ? ShellTexts.Forms.EditTitle : ShellTexts.Forms.NewTitle);

    // The shell's titles name the child; a configured title has no placeholders, so there it goes unused.
    private object TitleArguments => new { Name = Localizer.Text(Child.Singular) };

    private string CancelHref => Id is { } id ? ChildHref(id) : GeneratedChildForm.RootHref(Model, RootId);

    // The list step, the root (linked by the route's RootId, so it is a link before the load and beside the problem
    // panel), editing the child linked to its detail page, then the form: the edit command's label, or the add title.
    private IReadOnlyList<SerginBreadcrumb> Trail =>
    [
        .. ListStep(),
        RootStep(),
        .. ChildStep(),
        IsEdit
            ? new SerginBreadcrumb(Child.EditCommand!.Label)
            : SerginBreadcrumb.Data(Localizer.Text(TitleText, TitleArguments)),
    ];

    // Created on this view, so it re-renders after SubmitAsync; SerginFormView invokes it once every field is valid.
    private EventCallback OnSubmit => EventCallback.Factory.Create(this, SubmitAsync);

    protected override void OnInitialized()
    {
        Model = Views.For<TRoot>();
        Child = Model.Children.FirstOrDefault(candidate => candidate.Child == typeof(TChild))
            ?? throw new InvalidOperationException($"{typeof(TRoot).FullName} declares no view for the child {typeof(TChild).FullName}.");

        if (Model.GetOne is null || Model.Routes.Detail is null)
        {
            throw new InvalidOperationException(
                $"{typeof(TRoot).FullName} has no GetOne query or detail route, so the child form has no version to read and nowhere to leave for.");
        }

        if (IsEdit ? Child.Edit is null || Child.GetOne is null : Child.Add is null)
        {
            throw new InvalidOperationException(IsEdit
                ? $"{typeof(TChild).FullName} has no edit command and GetOne query, so its edit form has nothing to load and send."
                : $"{typeof(TChild).FullName} has no add command, so its add form has nothing to send.");
        }

        titleField = GeneratedViewSupport.TitleFieldOf(Child.DetailFields, Child.DetailTitleField);
        rootTitleField = GeneratedViewSupport.TitleFieldOf(Model.DetailFields, Model.DetailTitleField);

        // MudForm's form-level rule, for an input with a For (a child picker's parent select): the field by its path.
        validation = async (_, propertyName) => await ValidateAsync(propertyName);
    }

    protected override Task OnParametersSetAsync() => LoadAsync();

    private IEnumerable<SerginBreadcrumb> ListStep() =>
        GeneratedViewSupport.ListStep(Model, Catalog) is { } step ? [step] : [];

    private SerginBreadcrumb RootStep()
    {
        string href = GeneratedChildForm.RootHref(Model, RootId);

        return rootRecord is null
            ? new SerginBreadcrumb(Model.Singular, href)
            : SerginBreadcrumb.Data(GeneratedViewSupport.DisplayOf(rootRecord, rootTitleField, RootId), href);
    }

    private IEnumerable<SerginBreadcrumb> ChildStep()
    {
        if (Id is not { } id)
        {
            return [];
        }

        string href = ChildHref(id);

        return [record is null ? new SerginBreadcrumb(Child.Singular, href) : SerginBreadcrumb.Data(GeneratedViewSupport.DisplayOf(record, titleField, id), href)];
    }

    private string ChildHref(Guid id) =>
        Child.Routes.Detail?.Invoke(RootId, id) ?? GeneratedChildForm.RootHref(Model, RootId);

    // The command the form's values build: the one the field validation reads and the submit sends.
    private object ToCommand() => GeneratedChildForm.Build(Child, RootId, Id, values.Values);

    private async Task<IEnumerable<string>> ValidateAsync(string propertyName) =>
        await Invoker.ValidateAsync(FormValidator, ToCommand(), propertyName);

    // The root's version is read before the child, so a change to the child between the two reads makes it stale and the
    // submit is refused instead of overwriting what this view never showed. Called again after a stale save.
    private async Task LoadAsync()
    {
        ViewSendResult root = await Model.LoadRecordAsync(Dispatcher, RootId);

        if (root.IsError)
        {
            Clear();
            problem = ErrorPresenter.Present(root.Errors[0]);

            return;
        }

        rootRecord = root.Value;
        version = root.Version;

        if (!IsEdit)
        {
            problem = null;

            return;
        }

        ViewSendResult loaded = await Child.GetOne!.Invoker.SendAsync(Dispatcher, Child.GetOne.Build(RootId, Id!.Value));

        if (loaded.IsError)
        {
            Clear();
            problem = ErrorPresenter.Present(loaded.Errors[0]);

            return;
        }

        problem = null;
        object loadedRecord = loaded.Value!;
        record = loadedRecord;

        IReadOnlyDictionary<string, object?> stored = Child.DetailArguments!(loadedRecord);

        foreach (FormFieldView field in Fields)
        {
            values.Set(field.Name, stored.GetValueOrDefault(field.Name));
        }
    }

    // Nothing of the failed load shows: the trail falls back to the types' names, still linked by the route's ids.
    private void Clear()
    {
        record = null;
        rootRecord = null;
        version = null;
    }

    private async Task SubmitAsync()
    {
        if (!Ready)
        {
            return;
        }

        submitting = true;

        ChildSubmitOutcome outcome;

        try
        {
            outcome = await GeneratedChildForm.SubmitAsync(Dispatcher, Child, RootId, Id, values.Values, version);
        }
        finally
        {
            submitting = false;
        }

        switch (outcome.Status)
        {
            case ChildSubmitStatus.NoVersion:
                ErrorPresenter.Notify(Error.NotFound());
                await LoadAsync();

                return;
            case ChildSubmitStatus.Refused:
                // Every error, not the first: validation yields one per broken rule. Stale: show what is there now.
                ErrorPresenter.Notify(outcome.Errors);

                if (outcome.Stale)
                {
                    await LoadAsync();
                }

                return;
            default:
                await Host.LeaveAsync(GeneratedChildForm.LeaveTarget(Model, Child, RootId, Id, outcome.CreatedId));

                return;
        }
    }
}
