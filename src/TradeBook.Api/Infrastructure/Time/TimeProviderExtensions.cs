namespace TradeBook.Api.Infrastructure.Time;

public static class TimeProviderExtensions
{
    /// <summary>
    /// UTC now truncated to whole milliseconds, the precision of every
    /// <c>datetime2(3)</c> column in the schema. Stamping rows and messages
    /// with this rather than the raw clock means a timestamp echoed in a
    /// response or pushed over the hub is exactly what a later read returns,
    /// instead of differing in digits the database never stored.
    /// </summary>
    public static DateTime GetUtcNowToMilliseconds(this TimeProvider timeProvider)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        return new DateTime(utcNow.Ticks - utcNow.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
    }
}
