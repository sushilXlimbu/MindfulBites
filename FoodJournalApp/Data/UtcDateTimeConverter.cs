using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FoodJournalApp.Data;

// Store side: Local -> converted to UTC; Unspecified -> assumed to already be UTC
// (this app treats every DateTime as UTC). Read side: always Kind=Utc.
public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            v => ToUtc(v),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }

    private static DateTime ToUtc(DateTime v) => v.Kind switch
    {
        DateTimeKind.Utc => v,
        DateTimeKind.Local => v.ToUniversalTime(),
        _ => DateTime.SpecifyKind(v, DateTimeKind.Utc)
    };
}
