using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Sergin.SharedKernel.Application;
using Sergin.SharedKernel.Application.Tasks.Activity;
using Sergin.SharedKernel.Application.Tasks.Commands;
using Sergin.SharedKernel.Application.Tasks.Queries;
using Sergin.SharedKernel.Application.Tasks;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Tasks;

/// <summary>
/// The task services a host needs once any module opts in: the run-item executor and the store resolver.
/// <c>AddModuleDbContext</c> calls it from its opt-in branch, beside the context's own keyed store; a marker makes the
/// second opted-in context's call a no-op. The engine (<see cref="ITaskEngine"/>) is the adapter's to
/// register, and <see cref="TaskOptions"/> is bound and validated by <c>AddSerginCore</c> in every host.
/// <para>
/// Lives here rather than in <c>Sergin.SharedKernel.Hosts</c> because <c>AddModuleDbContext</c> is here, and this
/// project cannot reference Hosts (Hosts references it).
/// </para>
/// </summary>
public static class TaskServiceExtensions
{
    public static IServiceCollection AddSerginTasks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.Any(descriptor => descriptor.ServiceType == typeof(TaskServicesMarker)))
        {
            return services;
        }

        services.AddSingleton<TaskServicesMarker>();

        // Scoped: each holds the scope's stores, whose contexts are scoped. The executor opens its own child scope for
        // the send, so its bookkeeping context and the command's are never the same one.
        services.AddScoped<ITaskStoreResolver, TaskStoreResolver>();
        services.AddScoped<TaskRunItemExecutor>();
        services.AddScoped<TaskRunStarter>();
        services.AddScoped<ITaskScheduleRunner, TaskScheduleRunner>();

        // The management reads: SharedKernel's own handlers, which MediatR's scan of the modules' application assemblies
        // never reaches, and the SQL service they call.
        services.AddScoped<ITaskQueries, TaskQueries>();
        services.AddTransient<IRequestHandler<GetTaskSchedulesForTargetQuery, ErrorOr<IReadOnlyCollection<TaskScheduleListItem>>>, GetTaskSchedulesForTargetQueryHandler>();
        services.AddTransient<IRequestHandler<GetTaskScheduleByIdQuery, ErrorOr<TaskScheduleResponse>>, GetTaskScheduleByIdQueryHandler>();
        services.AddTransient<IRequestHandler<GetTaskScheduleRunsQuery, ErrorOr<ListQueryResponse<TaskRunListItem>>>, GetTaskScheduleRunsQueryHandler>();
        services.AddTransient<IRequestHandler<GetTaskRunItemsQuery, ErrorOr<IReadOnlyCollection<TaskRunItemListItem>>>, GetTaskRunItemsQueryHandler>();
        services.AddTransient<IRequestHandler<GetOneOffRunsForTargetQuery, ErrorOr<IReadOnlyCollection<TaskRunListItem>>>, GetOneOffRunsForTargetQueryHandler>();
        services.AddTransient<IRequestHandler<GetTaskSchedulesForCollectionQuery, ErrorOr<IReadOnlyCollection<TaskScheduleListItem>>>, GetTaskSchedulesForCollectionQueryHandler>();
        services.AddTransient<IRequestHandler<GetOneOffRunsForCollectionQuery, ErrorOr<IReadOnlyCollection<TaskRunListItem>>>, GetOneOffRunsForCollectionQueryHandler>();

        // A user's own runs across every opted-in module: the directory over the per-context sources, and its query
        // (AddSerginCore registers them too, for a host where nothing opts in).
        services.AddSerginRunActivity();

        // The management commands: their handlers and validators by hand, for the same reason; validators scoped, as
        // AddSerginCore registers a module's, so they live in the request scope the validation behavior runs in.
        services.AddScoped<TaskAuthority>();
        services.AddScoped<TaskArgumentCheck>();
        services.AddScoped<TaskEngineAccessor>();
        services.AddTransient<IRequestHandler<CreateTaskScheduleCommand, ErrorOr<CreateTaskScheduleCommandResponse>>, CreateTaskScheduleCommandHandler>();
        services.AddTransient<IRequestHandler<UpdateTaskScheduleCommand, ErrorOr<UpdateTaskScheduleCommandResponse>>, UpdateTaskScheduleCommandHandler>();
        services.AddTransient<IRequestHandler<PauseTaskScheduleCommand, ErrorOr<Success>>, PauseTaskScheduleCommandHandler>();
        services.AddTransient<IRequestHandler<ResumeTaskScheduleCommand, ErrorOr<Success>>, ResumeTaskScheduleCommandHandler>();
        services.AddTransient<IRequestHandler<DeleteTaskScheduleCommand, ErrorOr<Success>>, DeleteTaskScheduleCommandHandler>();
        services.AddTransient<IRequestHandler<RunTaskScheduleNowCommand, ErrorOr<TaskScheduleRunStartedResponse>>, RunTaskScheduleNowCommandHandler>();
        services.AddTransient<IRequestHandler<RunTaskOnceCommand, ErrorOr<TaskScheduleRunStartedResponse>>, RunTaskOnceCommandHandler>();
        services.AddTransient<IRequestHandler<StartTaskCommand, ErrorOr<TaskRunStartedResponse>>, StartTaskCommandHandler>();
        services.AddTransient<IRequestHandler<CancelTaskRunCommand, ErrorOr<Success>>, CancelTaskRunCommandHandler>();
        services.AddScoped<IValidator<CreateTaskScheduleCommand>, CreateTaskScheduleCommandValidator>();
        services.AddScoped<IValidator<UpdateTaskScheduleCommand>, UpdateTaskScheduleCommandValidator>();
        services.AddScoped<IValidator<RunTaskOnceCommand>, RunTaskOnceCommandValidator>();
        services.AddScoped<IValidator<StartTaskCommand>, StartTaskCommandValidator>();

        return services;
    }

    private sealed class TaskServicesMarker;
}
