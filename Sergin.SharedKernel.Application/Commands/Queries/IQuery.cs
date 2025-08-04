using MediatR;

namespace Sergin.SharedKernel.Application.Commands.Queries;
public interface IQuery<TResponse> : IRequest<ErrorOr<TResponse>>, IBaseCommand;
