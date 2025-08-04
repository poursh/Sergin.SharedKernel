using Sergin.SharedKernel.Application.Times;

namespace Sergin.HeadEnd.SharedKernel.Infrastructure.Times;

public class DefaultDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
