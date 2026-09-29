using System.Reflection;

namespace Sergin.SharedKernel.Application;

/// <summary>
/// Builds a pipeline behavior's TResponse from one <see cref="Error"/>, for the behaviors that short-circuit a
/// request: TResponse is only known as a type parameter, so an ErrorOr&lt;T&gt; is built through reflection.
/// </summary>
internal static class ErrorOrResponse
{
    public static bool TryFrom<TResponse>(Error error, out TResponse response)
    {
        if (typeof(TResponse) == typeof(IErrorOr))
        {
            response = (TResponse)(object)error;
            return true;
        }

        if (typeof(TResponse).IsGenericType && typeof(TResponse).GetGenericTypeDefinition() == typeof(ErrorOr<>))
        {
            Type resultType = typeof(TResponse).GetGenericArguments()[0];

            MethodInfo fromMethod = typeof(ErrorOr<>)
                .MakeGenericType(resultType)
                .GetMethod(nameof(ErrorOr<object>.From))!;

            response = (TResponse)fromMethod.Invoke(null, [new List<Error>([error])])!;
            return true;
        }

        response = default!;
        return false;
    }
}
