using MediatR;

namespace Sergin.SharedKernel.Application.Commands;

public interface ICommand : ICommand<Success>;
public interface ICommand<TResponse> : IRequest<ErrorOr<TResponse>>, IBaseCommand;

