namespace Sergin.SharedKernel.Application.Times;
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
