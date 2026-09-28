using Sergin.SharedKernel.Application.Times;

namespace Sergin.SharedKernel.Infrastructure.Times;

public sealed class SystemLocalTimeConverter : ILocalTimeConverter
{
    public DateTimeOffset ToLocal(DateTime utcDateTime, TimeZoneInfo zone)
    {
        var utc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        DateTime converted = TimeZoneInfo.ConvertTimeFromUtc(utc, zone);

        return new DateTimeOffset(converted, zone.GetUtcOffset(converted));
    }
}
