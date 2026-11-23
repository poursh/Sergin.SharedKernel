using Microsoft.Extensions.Logging;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Domain.Tasks;

namespace Sergin.SharedKernel.Application.Tasks.Commands;

/// <summary>Lets a paused schedule fire again. Version-guarded by its type.</summary>
public sealed record ResumeTaskScheduleCommand(string Schema, TaskScheduleId Id)
    : ICommand, ITransitionCommand<TaskSchedule, TaskScheduleId>, IReverseOf<PauseTaskScheduleCommand>;

internal sealed class ResumeTaskScheduleCommandHandler(ITaskStoreResolver stores, TaskEngineAccessor engines, ILogger<ResumeTaskScheduleCommandHandler> logger)
    : ICommandHandler<ResumeTaskScheduleCommand>
{
    public Task<ErrorOr<Success>> Handle(ResumeTaskScheduleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return TaskScheduleStateChange.ApplyAsync(stores.For(request.Schema), request.Id, enabled: true, engines.Engine, logger, cancellationToken);
    }
}
