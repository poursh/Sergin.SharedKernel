using System.Linq.Expressions;
using FluentValidation;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Localizations.Validation;
using Sergin.SharedKernel.Domain.ScheduledTasks;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Commands;

/// <summary>
/// The rules the create, update and run-once validators share. The template's own validator is not run: its
/// arguments are checked per item at run time, where the target is known. Two rules report under the scheduled-task
/// error codes (<see cref="ScheduledTaskErrors.NotSchedulable"/>, <see cref="ScheduledTaskErrors.TooManyTargets"/>)
/// rather than a property name, since the pipeline turns a failure's property name into the error's code; every
/// other rule reports under its property.
/// </summary>
internal static class ScheduledTaskRules
{
    private const string FallbackTimeZone = "UTC";

    public static void TemplateAndTargets<T>(
        AbstractValidator<T> validator,
        ScheduledCommandCatalog catalog,
        Func<T, string> schemaOf,
        Expression<Func<T, IBaseCommand>> template,
        Expression<Func<T, IReadOnlyCollection<ScheduledTaskTarget>>> targets)
    {
        Func<T, IBaseCommand?> templateOf = template.Compile();
        Func<T, IReadOnlyCollection<ScheduledTaskTarget>?> targetsOf = targets.Compile();

        // A command another module's catalogue entry owns is as unschedulable here as one that never opted in.
        ScheduledCommand? CommandOf(T request) =>
            templateOf(request) is { } value
            && catalog.For(value.GetType()) is { } command
            && (command.Schema is null || command.Schema == schemaOf(request))
                ? command
                : null;

        validator.RuleFor(template).NotNull();

        validator.RuleFor(template)
            .Must((request, _) => CommandOf(request) is not null)
            .OverridePropertyName(ScheduledTaskErrors.NotSchedulable.Code)
            .WithMessage(_ => ValidationTexts.Template(ScheduledTaskTexts.NotSchedulable))
            .When(request => templateOf(request) is not null);

        validator.RuleFor(targets).NotNull();

        validator.RuleFor(targets)
            .Must(picked => picked.Count > 0)
            .WithMessage(_ => ValidationTexts.Template(ScheduledTaskTexts.NoTargets))
            .When(request => targetsOf(request) is not null);

        // The targets' key is (task_id, target_id): one id twice, whatever its root, would collide.
        validator.RuleFor(targets)
            .Must(picked => picked.Select(target => target.TargetId).Distinct().Count() == picked.Count)
            .WithMessage(_ => ValidationTexts.Template(ScheduledTaskTexts.DuplicateTargets))
            .When(request => targetsOf(request) is not null);

        validator.RuleFor(targets)
            .Must((request, picked) => CommandOf(request) is not { RootIdType: not null } || picked.All(target => target.RootId is not null))
            .WithMessage(_ => ValidationTexts.Template(ScheduledTaskTexts.TargetWithoutRoot))
            .When(request => targetsOf(request) is not null);

        validator.RuleFor(targets)
            .Must((request, picked) => picked.Count <= 1 || CommandOf(request) is null or { Selectable: true })
            .OverridePropertyName(ScheduledTaskErrors.TooManyTargets.Code)
            .WithMessage(_ => ValidationTexts.Template(ScheduledTaskTexts.TooManyTargets))
            .When(request => targetsOf(request) is not null);
    }

    public static void Trigger<T>(
        AbstractValidator<T> validator,
        IScheduledTaskEngine engine,
        Expression<Func<T, string>> cron,
        Expression<Func<T, string>> timeZoneId)
    {
        Func<T, string?> cronOf = cron.Compile();
        Func<T, string?> timeZoneOf = timeZoneId.Compile();

        validator.RuleFor(cron).NotEmpty();

        // A cron is judged in its own time zone when that one is known, else in UTC: a bad zone is its own error.
        validator.RuleFor(cron)
            .Must((request, value) => engine.IsValidCron(value, IsKnownTimeZone(timeZoneOf(request)) ? timeZoneOf(request)! : FallbackTimeZone))
            .WithMessage(_ => ValidationTexts.Template(ScheduledTaskTexts.InvalidCron))
            .When(request => !string.IsNullOrWhiteSpace(cronOf(request)));

        validator.RuleFor(timeZoneId).NotEmpty();

        validator.RuleFor(timeZoneId)
            .Must(IsKnownTimeZone)
            .WithMessage(_ => ValidationTexts.Template(ScheduledTaskTexts.InvalidTimeZone))
            .When(request => !string.IsNullOrWhiteSpace(timeZoneOf(request)));
    }

    // IANA or Windows ids, as Sergin:TimeZone takes them.
    private static bool IsKnownTimeZone(string? id) =>
        !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);
}
