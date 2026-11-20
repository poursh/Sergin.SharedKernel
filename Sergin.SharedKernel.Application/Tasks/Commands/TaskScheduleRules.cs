using System.Linq.Expressions;
using FluentValidation;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Application.Localizations.Validation;
using Sergin.SharedKernel.Domain.Tasks;

namespace Sergin.SharedKernel.Application.Tasks.Commands;

/// <summary>
/// The rules the create, update and run-once validators share. The template's own validator is not run: its
/// arguments are checked per item at run time, where the target is known. Two rules report under the task
/// error codes (<see cref="TaskErrors.NotSchedulable"/>, <see cref="TaskErrors.TooManyTargets"/>)
/// rather than a property name, since the pipeline turns a failure's property name into the error's code; every
/// other rule reports under its property.
/// </summary>
internal static class TaskScheduleRules
{
    private const string FallbackTimeZone = "UTC";

    public static void TemplateAndTargets<T>(
        AbstractValidator<T> validator,
        TaskCatalog catalog,
        Func<T, string> schemaOf,
        Expression<Func<T, ITaskCommand>> template,
        Expression<Func<T, IReadOnlyCollection<TaskScheduleTarget>>> targets,
        bool schedulableOnly = true,
        string? targetsProperty = null)
    {
        Func<T, ITaskCommand?> templateOf = template.Compile();
        Func<T, IReadOnlyCollection<TaskScheduleTarget>?> targetsOf = targets.Compile();

        // A task another module's catalogue entry owns is as unschedulable here as one that is not Schedulable(), and so is
        // one no source declares. A start (schedulableOnly false) takes a task that is not Schedulable().
        TaskDescriptor? CommandOf(T request) =>
            templateOf(request) is { } value
            && catalog.ByRequest(value.GetType()) is { } command
            && (command.Settings.Schedulable || !schedulableOnly)
            && (command.Schema is null || command.Schema == schemaOf(request))
                ? command
                : null;

        validator.RuleFor(template).NotNull();

        validator.RuleFor(template)
            .Must((request, _) => CommandOf(request) is not null)
            .OverridePropertyName(TaskErrors.NotSchedulableCode)
            .WithMessage(_ => ValidationTexts.Template(TaskTexts.NotSchedulable))
            .When(request => templateOf(request) is not null);

        // A caller whose targets are converted (a start's) names the property, which the expression no longer can.
        IRuleBuilderOptions<T, TProperty> Named<TProperty>(IRuleBuilderOptions<T, TProperty> rule) =>
            targetsProperty is null ? rule : rule.OverridePropertyName(targetsProperty);

        Named(validator.RuleFor(targets).NotNull());

        Named(validator.RuleFor(targets).Must(picked => picked.Count > 0))
            .WithMessage(_ => ValidationTexts.Template(TaskTexts.NoTargets))
            .When(request => targetsOf(request) is not null);

        // The targets' key is (schedule_id, target_id): one id twice, whatever its root, would collide.
        Named(validator.RuleFor(targets).Must(picked => picked.Select(target => target.TargetId).Distinct().Count() == picked.Count))
            .WithMessage(_ => ValidationTexts.Template(TaskTexts.DuplicateTargets))
            .When(request => targetsOf(request) is not null);

        validator.RuleFor(targets)
            .Must((request, picked) => picked.Count <= 1 || CommandOf(request) is null or { Selectable: true })
            .OverridePropertyName(TaskErrors.TooManyTargets.Code)
            .WithMessage(_ => ValidationTexts.Template(TaskTexts.TooManyTargets))
            .When(request => targetsOf(request) is not null);
    }

    public static void Trigger<T>(
        AbstractValidator<T> validator,
        ITaskEngine engine,
        Expression<Func<T, string>> cron,
        Expression<Func<T, string>> timeZoneId)
    {
        Func<T, string?> cronOf = cron.Compile();
        Func<T, string?> timeZoneOf = timeZoneId.Compile();

        validator.RuleFor(cron).NotEmpty();

        // A cron is judged in its own time zone when that one is known, else in UTC: a bad zone is its own error.
        validator.RuleFor(cron)
            .Must((request, value) => engine.IsValidCron(value, IsKnownTimeZone(timeZoneOf(request)) ? timeZoneOf(request)! : FallbackTimeZone))
            .WithMessage(_ => ValidationTexts.Template(TaskTexts.InvalidCron))
            .When(request => !string.IsNullOrWhiteSpace(cronOf(request)));

        validator.RuleFor(timeZoneId).NotEmpty();

        validator.RuleFor(timeZoneId)
            .Must(IsKnownTimeZone)
            .WithMessage(_ => ValidationTexts.Template(TaskTexts.InvalidTimeZone))
            .When(request => !string.IsNullOrWhiteSpace(timeZoneOf(request)));
    }

    // IANA or Windows ids, as Sergin:TimeZone takes them.
    private static bool IsKnownTimeZone(string? id) =>
        !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);
}
