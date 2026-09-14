using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TradeBook.Api.Persistence;

/// <summary>
/// Writes a <see cref="DateTime"/> unchanged and reads it back with
/// <see cref="DateTimeKind.Utc"/>. Storage is datetime2, which has no offset,
/// so this is the only place the UTC convention can be re-asserted on load.
/// </summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            toStore => toStore,
            fromStore => DateTime.SpecifyKind(fromStore, DateTimeKind.Utc))
    {
    }
}
