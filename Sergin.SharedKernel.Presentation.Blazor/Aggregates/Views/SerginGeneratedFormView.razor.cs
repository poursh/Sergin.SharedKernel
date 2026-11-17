using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Commands.Types;
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
/// The create (no <see cref="Id"/>) or edit form of <typeparamref name="TRoot"/>, generated from its
/// <see cref="AggregateViewModel"/>: an input per shown field of its create or update command, validated field by field
/// by the pipeline's own validator over the command built from the form (<c>BuildCreate</c> / <c>BuildUpdate</c>, the
/// same build the submit sends, so the two cannot drift).
/// </summary>
/// <remarks>
/// An edit form loads the record through the GetOne query (at its version when the root is <c>Versioned()</c>) and renders
/// only once the record is loaded, each field prefilled from the record's property of the same name and a child picker's parent from its
/// <c>ParentFromRecord</c>. A create is sent and leaves for the new record's detail page; an edit is sent at the held
/// version when the update is version-guarded, leaves for the detail page, and on a stale answer reads the record again.
/// The title is the form's configured <c>Title(...)</c>, else "New {Name}" / "Edit {Name}" with the root's name. A create
/// command with a parent property (<see cref="AggregateViewModel.CreateParent"/>) takes it from <see cref="ParentId"/> and
/// renders no input for it; for a root that belongs to a parent (<c>BelongsTo</c>), the trail and Cancel then name the parent.
/// </remarks>
public sealed partial class SerginGeneratedFormView<TRoot>
    where TRoot : class, IAggregateRoot
{
    private readonly GeneratedFormValues values = new();

    private object? record;
    private RowVersion? version;
    private SerginProblem? problem;
    private bool submitting;
    private FieldView? titleField;
    private AggregateViewModel? belongsToModel;
    private Func<object, string, Task<IEnumerable<string>>> validation = default!;

    /// <summary>The record to edit; null for a create form.</summary>
    [Parameter]
    public Guid? Id { get; set; }

    /// <summary>
    /// A create form's parent: the id its parent property (<c>Form&lt;T&gt;(form =&gt; form.Parent(...))</c>, or <c>ParentId</c>
    /// of a create command under a parent) is filled from, read by the page from its route or query. Required when the
    /// create command has a parent property; ignored by an edit form.
    /// </summary>
    [Parameter]
    public Guid? ParentId { get; set; }

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

    private bool IsEdit => Id is not null;

    private IReadOnlyList<FormFieldView> Fields => IsEdit ? Model.UpdateFields : Model.CreateFields;

    private RequestInvoker Invoker => (IsEdit ? Model.Update : Model.Create)!.Invoker;

    // An edit form waits for the record; its version is held only for a Versioned() root, so it is not what gates.
    private bool Ready => !IsEdit || record is not null;

    private TextDefinition TitleText =>
        (IsEdit ? Model.UpdateTitle : Model.CreateTitle) ?? (IsEdit ? ShellTexts.Forms.EditTitle : ShellTexts.Forms.NewTitle);

    // The shell's titles name the root; a configured title has no placeholders, so there it goes unused.
    private object TitleArguments => new { Name = Localizer.Text(Model.Singular) };

    private string CancelHref => Id is { } id ? DetailHref(id) : CreateCancelHref;

    // A create form under a parent the record belongs to cancels to the parent.
    private string CreateCancelHref =>
        belongsToModel is not null && CreateParentId is { } parentId
            ? GeneratedViewSupport.ParentLeaveHref(belongsToModel, parentId)
            : Model.Routes.List ?? "/";

    // The parent a create form is given; an edit form has none.
    // The node the surrounding tree selects: the record on edit, the new record's parent on create (none selects nothing).
    private string? TreeNode => IsEdit ? Id?.ToString() : ParentId?.ToString();

    private Guid? CreateParentId => IsEdit || Model.CreateParent is null ? null : ParentId;

    // The list section step, then — editing — the record linking to its detail page (the root's name until it loads),
    // then the form: Edit, or the create form's title. A create form under a parent the record belongs to has the parent's
    // list step and the parent (by its singular, linking to its detail) in place of the list step.
    private IReadOnlyList<SerginBreadcrumb> Trail =>
    [
        .. belongsToModel is not null && CreateParentId is { } parentId
            ? GeneratedViewSupport.ParentSteps(belongsToModel, Catalog, parentId, parentName: null)
            : ListStep(),
        .. RecordStep(),
        IsEdit ? new SerginBreadcrumb(CommonTexts.Edit) : SerginBreadcrumb.Data(Localizer.Text(TitleText, TitleArguments)),
    ];

    // Created on this view, so it re-renders after SubmitAsync; SerginFormView invokes it once every field is valid.
    private EventCallback OnSubmit => EventCallback.Factory.Create(this, SubmitAsync);

    protected override void OnInitialized()
    {
        Model = Views.For<TRoot>();

        bool missing = IsEdit
            ? Model.Update is null || Model.GetOne is null
            : Model.Create is null;

        if (missing)
        {
            throw new InvalidOperationException(IsEdit
                ? $"{typeof(TRoot).FullName} has no IUpdateCommand and IGetOneQuery, so its edit form has nothing to load and send."
                : $"{typeof(TRoot).FullName} has no ICreateCommand, so its create form has nothing to send.");
        }

        titleField = GeneratedViewSupport.TitleFieldOf(Model.DetailFields, Model.DetailTitleField);
        belongsToModel = Model.BelongsTo is { } belongsTo ? Views.Find(belongsTo.Parent) : null;

        // MudForm's form-level rule, for an input with a For (a child picker's parent select): the field by its path.
        validation = async (_, propertyName) => await ValidateAsync(propertyName);
    }

    protected override Task OnParametersSetAsync()
    {
        if (IsEdit)
        {
            return LoadAsync();
        }

        // The parent property is never rendered: the page's ParentId fills it, as a Guid the request factory wraps.
        if (Model.CreateParent is { } parentProperty)
        {
            Guid parentId = ParentId ?? throw new InvalidOperationException(
                $"{typeof(TRoot).FullName}'s create command takes its parent's id in {parentProperty}, so its create form needs "
                + "ParentId: set it from the page's route or query.");
            values.Set(parentProperty, parentId);
        }

        return Task.CompletedTask;
    }

    private IEnumerable<SerginBreadcrumb> ListStep() =>
        GeneratedViewSupport.ListStep(Model, Catalog) is { } step ? [step] : [];

    // Links by the route id, so it is a link on the not-found path too.
    private IEnumerable<SerginBreadcrumb> RecordStep()
    {
        if (Id is not { } id)
        {
            return [];
        }

        string href = DetailHref(id);
        string? display = record is null || titleField is null ? null : titleField.Get(record)?.ToString();

        return [display is null ? new SerginBreadcrumb(Model.Singular, href) : SerginBreadcrumb.Data(display, href)];
    }

    private string DetailHref(Guid id) => Model.Routes.Detail?.Invoke(id) ?? Model.Routes.List ?? "/";

    // The command the form's values build: the one the field validation reads and the submit sends.
    private object ToCommand() => Model.BuildForm(Id, values.Values);

    private async Task<IEnumerable<string>> ValidateAsync(string propertyName) =>
        await Invoker.ValidateAsync(FormValidator, ToCommand(), propertyName);

    // Fills the form from the stored record and keeps the version it was read at. Called again after a stale save, so
    // the next submit is against what is there now.
    private async Task LoadAsync()
    {
        ViewSendResult loaded = await Model.LoadRecordAsync(Dispatcher, Id!.Value);

        if (loaded.IsError)
        {
            record = null;
            version = null;
            problem = ErrorPresenter.Present(loaded.Errors[0]);

            return;
        }

        problem = null;
        object loadedRecord = loaded.Value!;
        record = loadedRecord;
        version = loaded.Version;

        IReadOnlyDictionary<string, object?> stored = Model.DetailArguments!(loadedRecord);

        foreach (FormFieldView field in Fields)
        {
            values.Set(field.Name, stored.GetValueOrDefault(field.Name));

            if (field.ParentFromRecord is { } parentOf)
            {
                values.SetParent(field.Name, parentOf(loadedRecord));
            }
        }
    }

    // Internal for the parent and field-component tests, which submit without a form around the view.
    internal async Task SubmitAsync()
    {
        if (IsEdit && record is null)
        {
            return;
        }

        submitting = true;

        ViewSendResult result = await Model.SubmitFormAsync(Dispatcher, Id, values.Values, version);

        submitting = false;

        // Every error, not the first: validation yields one per broken rule. A stale edit means someone changed the
        // record after this view loaded it: show what is there now, with its version.
        if (await GeneratedViewSupport.FailedAsync(result, ErrorPresenter, () => IsEdit ? LoadAsync() : Task.CompletedTask))
        {
            return;
        }

        Guid? target = Id ?? (result.Value as ICreatedResponse)?.Id;

        await Host.LeaveAsync(target is { } id ? DetailHref(id) : Model.Routes.List ?? "/");
    }
}
