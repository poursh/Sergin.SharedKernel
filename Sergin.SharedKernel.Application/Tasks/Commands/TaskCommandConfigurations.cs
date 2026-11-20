using Sergin.SharedKernel.Application.Commands.Configuration;

namespace Sergin.SharedKernel.Application.Tasks.Commands;

// Every management command requires Manage. The typed ones take their scope (current: TaskSchedule is Scoped()) and
// version rule (Update, Pause, Resume, Delete: TaskSchedule is Versioned()) from their type; RunTaskScheduleNow and
// RunTaskOnce are untyped, so checked on the current scope and never versioned, and declare no
// WithoutExpectedVersion(), which the registry would refuse as waiving nothing. No property is a field a person
// sees on a form of these records: the shared UI labels its own controls, so each is NotShown.

internal sealed class CreateTaskScheduleCommandConfiguration : ICommandConfiguration<CreateTaskScheduleCommand>
{
    public void Configure(CommandConfigurationBuilder<CreateTaskScheduleCommand> builder) =>
        builder.RequirePermissions(TaskPermissions.Manage)
            .NotShown(x => x.Schema).NotShown(x => x.Template).NotShown(x => x.Targets).NotShown(x => x.Cron).NotShown(x => x.TimeZoneId);
}

internal sealed class UpdateTaskScheduleCommandConfiguration : ICommandConfiguration<UpdateTaskScheduleCommand>
{
    public void Configure(CommandConfigurationBuilder<UpdateTaskScheduleCommand> builder) =>
        builder.RequirePermissions(TaskPermissions.Manage)
            .NotShown(x => x.Schema).NotShown(x => x.Template).NotShown(x => x.Targets).NotShown(x => x.Cron).NotShown(x => x.TimeZoneId);
}

internal sealed class PauseTaskScheduleCommandConfiguration : ICommandConfiguration<PauseTaskScheduleCommand>
{
    public void Configure(CommandConfigurationBuilder<PauseTaskScheduleCommand> builder) =>
        builder.RequirePermissions(TaskPermissions.Manage).NotShown(x => x.Schema);
}

internal sealed class ResumeTaskScheduleCommandConfiguration : ICommandConfiguration<ResumeTaskScheduleCommand>
{
    public void Configure(CommandConfigurationBuilder<ResumeTaskScheduleCommand> builder) =>
        builder.RequirePermissions(TaskPermissions.Manage).NotShown(x => x.Schema);
}

internal sealed class DeleteTaskScheduleCommandConfiguration : ICommandConfiguration<DeleteTaskScheduleCommand>
{
    public void Configure(CommandConfigurationBuilder<DeleteTaskScheduleCommand> builder) =>
        builder.RequirePermissions(TaskPermissions.Manage).NotShown(x => x.Schema);
}

internal sealed class RunTaskScheduleNowCommandConfiguration : ICommandConfiguration<RunTaskScheduleNowCommand>
{
    public void Configure(CommandConfigurationBuilder<RunTaskScheduleNowCommand> builder) =>
        builder.RequirePermissions(TaskPermissions.Manage).NotShown(x => x.Schema).NotShown(x => x.Id);
}

internal sealed class RunTaskOnceCommandConfiguration : ICommandConfiguration<RunTaskOnceCommand>
{
    public void Configure(CommandConfigurationBuilder<RunTaskOnceCommand> builder) =>
        builder.RequirePermissions(TaskPermissions.Manage)
            .NotShown(x => x.Schema).NotShown(x => x.Template).NotShown(x => x.Targets);
}

// The one start that needs no Manage: the handler checks the background command's own permissions, as the pipeline
// would check them for the command itself.
internal sealed class StartTaskCommandConfiguration : ICommandConfiguration<StartTaskCommand>
{
    public void Configure(CommandConfigurationBuilder<StartTaskCommand> builder) =>
        builder.NotShown(x => x.Template).NotShown(x => x.Targets);
}

// No permission either: the handler lets the run's acting user cancel, and otherwise checks Manage on the run's scope,
// which a configuration cannot name.
internal sealed class CancelTaskRunCommandConfiguration : ICommandConfiguration<CancelTaskRunCommand>
{
    public void Configure(CommandConfigurationBuilder<CancelTaskRunCommand> builder) =>
        builder.NotShown(x => x.Schema).NotShown(x => x.RunId);
}
