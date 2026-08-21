namespace Sergin.SharedKernel.Presentation.Grpc.Errors;

public static class ErrorReplyExtensions
{
    public static ErrorOr<T> ToErrorOr<T>(this ErrorReply reply) =>
        Error.Custom((int)reply.Type, reply.Code, reply.Description);

    public static ErrorReply ToErrorReply(this Error error) =>
        new()
        {
            Code = error.Code,
            Description = error.Description,
            Type = (ProtoErrorType)(int)error.Type,
        };
}
