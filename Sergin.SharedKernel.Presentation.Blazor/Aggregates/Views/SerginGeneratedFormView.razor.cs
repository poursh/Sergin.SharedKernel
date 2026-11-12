using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Modules;
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
/// An edit form loads the record through the GetOne query at its version and renders only once a version is held, each
/// field prefilled from the record's property of the same name and a child picker's parent from its
/// <c>ParentFromRecord</c>. A create is sent and leaves for the new record's detail page; an edit is sent at the held
/// version when the update is version-guarded, leaves for the detail page, and on a stale answer reads the record again.
/// The title is the form's configured <c>Title(...)</c>, else "New {Name}" / "Edit {Name}" with the root's name.
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
    private Func<object, string, Task<IEnumerable<string>>> validation = default!;

    /// <summary>The record to edit; null for a create form.</summary>
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

    private bool IsEdit => Id is not null;

    private IReadOnlyList<FormFieldView> Fields => IsEdit ? Model.UpdateFields : Model.CreateFields;

    private RequestInvoker Invoker => (IsEdit ? Model.UpdateInvoker : Model.CreateInvoker)!;

    private bool Ready => !IsEdit || version is not null;

    private TextDefinition TitleText =>
        (IsEdit ? Model.UpdateTitle : Model.CreateTitle) ?? (IsEdit ? ShellTexts.Forms.EditTitle : ShellTexts.Forms.NewTitle);

    // The shell's titles name the root; a configured title has no placeholders, so there it goes unused.
    private object TitleArguments => new { Name = Localizer.Text(Model.Singular) };

    private string CancelHref => Id is { } id ? DetailHref(id) : Model.Routes.List ?? "/";

    // The list section step, then — editing — the record linking to its detail page (the root's name until it loads),
    // then the form: Edit, or the create form's title.
    private IReadOnlyList<SerginBreadcrumb> Trail =>
    [
        .. ListStep(),
        .. RecordStep(),
        IsEdit ? new SerginBreadcrumb(CommonTexts.Edit) : SerginBreadcrumb.Data(Localizer.Text(TitleText, TitleArguments)),
    ];

    // Created on this view, so it re-renders after SubmitAsync; SerginFormView invokes it once every field is valid.
    private EventCallback OnSubmit => EventCallback.Factory.Create(this, SubmitAsync);

    protected override void OnInitialized()
    {
        Model = Views.For<TRoot>();

        bool missing = IsEdit
            ? Model.UpdateInvoker is null || Model.BuildUpdate is null || Model.GetOneInvoker is null || Model.BuildGetOne is null
            : Model.CreateInvoker is null || Model.BuildCreate is null;

        if (missing)
        {
            throw new InvalidOperationException(IsEdit
                ? $"{typeof(TRoot).FullName} has no IUpdateCommand and IGetOneQuery, so its edit form has nothing to load and send."
                : $"{typeof(TRoot).FullName} has no ICreateCommand, so its create form has nothing to send.");
        }

        titleField = Model.DetailFields.FirstOrDefault(candidate => candidate.Name == Model.DetailTitleField);

        // MudForm's form-level rule, for an input with a For (a child picker's parent select): the field by its path.
        validation = async (_, propertyName) => await ValidateAsync(propertyName);
    }

    protected override Task OnParametersSetAsync() => IsEdit ? LoadAsync() : Task.CompletedTask;

    private IEnumerable<SerginBreadcrumb> ListStep()
    {
        if (Model.Routes.List is not { } list)
        {
            return [];
        }

        SerginNavItem? item = Catalog.NavItems.FirstOrDefault(candidate => string.Equals(candidate.Href, list, StringComparison.OrdinalIgnoreCase));

        return [item is null ? new SerginBreadcrumb(Model.Title, list) : SerginBreadcrumb.Of(item)];
    }

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
    private object ToCommand() =>
        Id is { } id ? Model.BuildUpdate!(id, values.Values) : Model.BuildCreate!(Guid.Empty, values.Values);

    private async Task<IEnumerable<string>> ValidateAsync(string propertyName) =>
        await Invoker.ValidateAsync(FormValidator, ToCommand(), propertyName);

    // Fills the form from the stored record and keeps the version it was read at. Called again after a stale save, so
    // the next submit is against what is there now.
    private async Task LoadAsync()
    {
        ViewSendResult loaded = await Model.GetOneInvoker!.SendVersionedAsync(Dispatcher, Model.BuildGetOne!(Id!.Value), expected: null);

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

    private async Task SubmitAsync()
    {
        if (IsEdit && version is null)
        {
            return;
        }

        bool versioned = IsEdit && Model.Commands.Any(command => command.Request == Model.Update && command.RequiresVersion);

        submitting = true;

        object request = ToCommand();
        ViewSendResult result = versioned
            ? await Invoker.SendVersionedAsync(Dispatcher, request, version)
            : await Invoker.SendAsync(Dispatcher, request);

        submitting = false;

        if (result.IsError)
        {
            // Every error, not the first: validation yields one per broken rule.
            ErrorPresenter.Notify(result.Errors);

            // Someone changed the record after this view loaded it: show what is there now, with its version.
            if (IsEdit && result.Errors.Any(VersionErrors.IsStale))
            {
                await LoadAsync();
            }

            return;
        }

        Guid? target = Id ?? (result.Value as ICreatedResponse)?.Id;

        await Host.LeaveAsync(target is { } id ? DetailHref(id) : Model.Routes.List ?? "/");
    }
}
