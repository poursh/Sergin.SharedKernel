using Sergin.SharedKernel.Application.Commands.Configuration;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Commands;

// Every management command requires Manage. The typed ones take their scope (current: ScheduledTask is Scoped()) and
// version rule (Update, Pause, Resume, Delete: ScheduledTask is Versioned()) from their type; RunScheduledTaskNow and
// RunCommandOnce are untyped, so checked on the current scope and never versioned, and declare no
// WithoutExpectedVersion(), which the registry would refuse as waiving nothing. No property is a field a person
// sees on a form of these records: the shared UI labels its own controls, so each is NotShown.

internal sealed class CreateScheduledTaskCommandConfiguration : ICommandConfiguration<CreateScheduledTaskCommand>
{
    public void Configure(CommandConfigurationBuilder<CreateScheduledTaskCommand> builder) =>
        builder.RequirePermissions(ScheduledTaskPermissions.Manage)
            .NotShown(x => x.Schema).NotShown(x => x.Template).NotShown(x => x.Targets).NotShown(x => x.Cron).NotShown(x => x.TimeZoneId);
}

internal sealed class UpdateScheduledTaskCommandConfiguration : ICommandConfiguration<UpdateScheduledTaskCommand>
{
    public void Configure(CommandConfigurationBuilder<UpdateScheduledTaskCommand> builder) =>
        builder.RequirePermissions(ScheduledTaskPermissions.Manage)
            .NotShown(x => x.Schema).NotShown(x => x.Template).NotShown(x => x.Targets).NotShown(x => x.Cron).NotShown(x => x.TimeZoneId);
}

internal sealed class PauseScheduledTaskCommandConfiguration : ICommandConfiguration<PauseScheduledTaskCommand>
{
    public void Configure(CommandConfigurationBuilder<PauseScheduledTaskCommand> builder) =>
        builder.RequirePermissions(ScheduledTaskPermissions.Manage).NotShown(x => x.Schema);
}

internal sealed class ResumeScheduledTaskCommandConfiguration : ICommandConfiguration<ResumeScheduledTaskCommand>
{
    public void Configure(CommandConfigurationBuilder<ResumeScheduledTaskCommand> builder) =>
        builder.RequirePermissions(ScheduledTaskPermissions.Manage).NotShown(x => x.Schema);
}

internal sealed class DeleteScheduledTaskCommandConfiguration : ICommandConfiguration<DeleteScheduledTaskCommand>
{
    public void Configure(CommandConfigurationBuilder<DeleteScheduledTaskCommand> builder) =>
        builder.RequirePermissions(ScheduledTaskPermissions.Manage).NotShown(x => x.Schema);
}

internal sealed class RunScheduledTaskNowCommandConfiguration : ICommandConfiguration<RunScheduledTaskNowCommand>
{
    public void Configure(CommandConfigurationBuilder<RunScheduledTaskNowCommand> builder) =>
        builder.RequirePermissions(ScheduledTaskPermissions.Manage).NotShown(x => x.Schema).NotShown(x => x.Id);
}

internal sealed class RunCommandOnceCommandConfiguration : ICommandConfiguration<RunCommandOnceCommand>
{
    public void Configure(CommandConfigurationBuilder<RunCommandOnceCommand> builder) =>
        builder.RequirePermissions(ScheduledTaskPermissions.Manage)
            .NotShown(x => x.Schema).NotShown(x => x.Template).NotShown(x => x.Targets);
}
