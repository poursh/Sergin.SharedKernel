using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using Sergin.SharedKernel.Application.Securities.Users;

namespace Sergin.SharedKernel.Presentation.Blazor.Validation;

/// <summary>
/// Opens one fresh DI scope per call and resolves the pipeline's <c>IValidator&lt;TRequest&gt;</c>
/// inside it. The validators are registered scoped and take repositories, and in Blazor Server
/// "scoped" is the circuit's lifetime — resolving one off the circuit's container would share a
/// DbContext for as long as the tab is open, the same hazard
/// <see cref="Dispatching.ScopedSerginDispatcher"/> exists for.
/// </summary>
/// <remarks>
/// Registered <b>scoped</b>, for the dispatcher's reason: the scope this opens comes from the root
/// provider and can build no user of its own, so the caller's <see cref="IUserContext"/> is seeded
/// into it through <see cref="UserContextAccessor"/>. No validator reads the user today; seeding it
/// keeps a future user-aware rule behaving identically here and in the pipeline.
/// </remarks>
internal sealed class ScopedSerginFormValidator(IServiceScopeFactory scopeFactory, IUserContext userContext)
    : ISerginFormValidator
{
    public async Task<IReadOnlyCollection<string>> ValidateAsync<TRequest>(
        TRequest request, string propertyName, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<UserContextAccessor>().Current = userContext;

        IValidator<TRequest>? validator = scope.ServiceProvider.GetService<IValidator<TRequest>>();

        if (validator is null)
        {
            return [];
        }

        var context = ValidationContext<TRequest>.CreateWithOptions(
            request, options => options.IncludeProperties(propertyName));

        ValidationResult result = await validator.ValidateAsync(context, cancellationToken);

        return [.. result.Errors.Select(error => error.ErrorMessage)];
    }
}
