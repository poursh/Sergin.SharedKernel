using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Application.Tasks.Commands;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Blazor.Modules;
using Sergin.SharedKernel.Presentation.Blazor.Navigation;
using Sergin.SharedKernel.Presentation.Blazor.Tasks;
using Sergin.SharedKernel.Presentation.Blazor.Validation;
using Sergin.SharedKernel.Presentation.Blazor.Views;
using Sergin.SharedKernel.Presentation.Errors;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views;

/// <summary>
/// The argument form of <typeparamref name="TTask"/>, a task of <typeparamref name="TRoot"/>, generated from the root's
/// <see cref="AggregateViewModel"/>: an input per argument (<see cref="CommandView.TaskFields"/>), each checked field by
/// field by the pipeline's own validator over the task built from the form, and Start, which starts the task with the
/// values. A task over one record is started over <see cref="Id"/> (or each of <see cref="Ids"/>, with the same values); a
/// task over the collection over the current scope, with no record.
/// </summary>
/// <remarks>
/// In <see cref="SerginEditMode.Page"/> (the default: the shape a module's routable page hosts at the task's
/// <c>PageRoute(...)</c>) it is a <see cref="SerginFormView"/>: the trail is the list step, the record linking to its detail
/// (its title once loaded through the GetOne query, the root's name until then), then the task's label; Start leaves to
/// the record (to the list for a task over the collection) and Cancel leaves there without starting. In
/// <see cref="SerginEditMode.Inline"/> it is the task's section (<see cref="GeneratedTaskSection"/>), and in
/// <see cref="SerginEditMode.Dialog"/> the bare form, both staying put after Start with the values kept. A failed load
/// renders the problem panel in place of the form.
/// </remarks>
public sealed partial class SerginGeneratedTaskFormView<TRoot, TTask> : IDisposable
    where TRoot : class, IAggregateRoot
    where TTask : ITaskCommand
{
    private object? record;
    private SerginProblem? problem;
    private bool starting;
    private FieldView? titleField;
    private bool watchingActivity;
    private volatile bool disposed;
    private Func<object, string, Task<IEnumerable<string>>> validation = default!;

    /// <summary>The record a task over one record is started over; ignored for a task over the collection.</summary>
    [Parameter]
    public Guid? Id { get; set; }

    /// <summary>The records a task over one record is started over with the same values, when <see cref="Id"/> is not set.</summary>
    [Parameter]
    public IReadOnlyCollection<Guid>? Ids { get; set; }

    /// <summary>How the form is shown; <see cref="SerginEditMode.Page"/> by default.</summary>
    [Parameter]
    public SerginEditMode Mode { get; set; } = SerginEditMode.Page;

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

    [Inject]
    private ICultureFormatter Formatter { get; set; } = default!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = default!;

    [Inject]
    private ActivityPanelState ActivityPanel { get; set; } = default!;

    [Inject]
    private IAuthorizationScopeContext CurrentScope { get; set; } = default!;

    [CascadingParameter]
    private SerginViewHost Host { get; set; } = SerginViewHost.Unhosted;

    private AggregateViewModel Model { get; set; } = default!;

    private CommandView Command { get; set; } = default!;

    // Internal, not private: the page tests set the values the inputs would.
    internal GeneratedTaskArguments Arguments { get; private set; } = default!;

    private bool OverRecords => Command.Role == CommandRole.Task;

    // A task over one record waits for the record it names, when the root can load it.
    private bool Ready => problem is null && (!OverRecords || Id is null || Model.GetOne is null || record is not null);

    private IReadOnlyCollection<TaskTarget> Targets
    {
        get
        {
            if (!OverRecords)
            {
                return [];
            }

            return Id is { } id ? [new TaskTarget(id, Display(id))] : [.. (Ids ?? []).Select(target => new TaskTarget(target))];
        }
    }

    private bool Busy =>
        Command.Task is { } task
        && (OverRecords
            ? Id is { } id && ActivityPanel.IsRunning(task.Name, id, scopeId: null)
            : ActivityPanel.IsRunning(task.Name, targetId: null, CurrentScope.ScopeId));

    // The record a task over one record leaves to, else the list, else the home page.
    private string LeaveHref =>
        (OverRecords && Id is { } id ? Model.Routes.Detail?.Invoke(id) : null) ?? Model.Routes.List ?? "/";

    // The list section step, the record linking to its detail (by the route id, so it links on the not-found path too),
    // then the task.
    private IReadOnlyList<SerginBreadcrumb> Trail =>
    [
        .. GeneratedViewSupport.ListStep(Model, Catalog) is { } list ? [list] : Array.Empty<SerginBreadcrumb>(),
        .. RecordStep(),
        new SerginBreadcrumb(Command.Label),
    ];

    // Created on this view, so it re-renders after SubmitAsync; SerginFormView invokes it once every field is valid.
    private EventCallback OnSubmit => EventCallback.Factory.Create(this, SubmitAsync);

    protected override void OnInitialized()
    {
        Model = Views.For<TRoot>();
        Command = Model.Commands.SingleOrDefault(command => command.Request == typeof(TTask))
            ?? throw new InvalidOperationException($"{typeof(TTask).FullName} is not a command of {typeof(TRoot).FullName}'s views.");

        if (Command is not { Role: CommandRole.Task or CommandRole.CollectionTask, Hidden: false })
        {
            throw new InvalidOperationException($"{typeof(TTask).FullName} is not a shown task of {typeof(TRoot).FullName}, so it has no argument form.");
        }

        Arguments = new GeneratedTaskArguments(Command, FormValidator);
        titleField = GeneratedViewSupport.TitleFieldOf(Model.DetailFields, Model.DetailTitleField);
        validation = async (_, propertyName) => await ValidateAsync(propertyName);

        if (Command.Task is not null)
        {
            ActivityPanel.RowsChanged += OnActivityChanged;
            watchingActivity = true;
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (OverRecords && Id is null && Ids is not { Count: > 0 })
        {
            throw new InvalidOperationException(
                $"{typeof(TTask).FullName} is a task over one record, so its form needs the record: set Id (or Ids).");
        }

        record = null;
        problem = null;

        if (OverRecords && Id is { } id && Model.GetOne is not null)
        {
            ViewSendResult loaded = await Model.LoadRecordAsync(Dispatcher, id);

            if (loaded.IsError)
            {
                problem = ErrorPresenter.Present(loaded.Errors[0]);
                return;
            }

            record = loaded.Value;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        disposed = true;

        if (watchingActivity)
        {
            ActivityPanel.RowsChanged -= OnActivityChanged;
            watchingActivity = false;
        }
    }

    // Raised by the indicator, which must not be broken by a subscriber: a re-render is queued, never thrown into it.
    private void OnActivityChanged()
    {
        if (!disposed)
        {
            _ = RerenderAsync();
        }
    }

    private async Task RerenderAsync()
    {
        try
        {
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception exception) when (exception is ObjectDisposedException or InvalidOperationException)
        {
            // The renderer went away between the check and the call: nothing is left to draw.
        }
    }

    private string Display(Guid id) => GeneratedViewSupport.DisplayOf(record, titleField, id);

    private IEnumerable<SerginBreadcrumb> RecordStep()
    {
        if (!OverRecords || Id is not { } id)
        {
            return [];
        }

        string? href = Model.Routes.Detail?.Invoke(id);
        string? display = record is null || titleField is null ? null : titleField.Get(record)?.ToString();

        return [display is null ? new SerginBreadcrumb(Model.Singular, href) : SerginBreadcrumb.Data(display, href)];
    }

    private Task<IEnumerable<string>> ValidateAsync(string propertyName) =>
        Arguments.ValidateAsync(propertyName, Targets.FirstOrDefault()?.TargetId);

    // The page's Start, once SerginFormView found every field valid. Internal for the page tests, which submit without a
    // form around the view.
    internal async Task SubmitAsync()
    {
        if (!Ready || starting)
        {
            return;
        }

        starting = true;
        ViewSendResult result;

        try
        {
            result = await Arguments.StartAsync(Dispatcher, Targets);
        }
        finally
        {
            starting = false;
        }

        if (result.IsError)
        {
            ErrorPresenter.Notify(result.Errors);
            return;
        }

        GeneratedTaskStarts.Notify(ActivityPanel, Snackbar, Localizer, Formatter, Targets.Count);
        await Host.LeaveAsync(LeaveHref);
    }
}
