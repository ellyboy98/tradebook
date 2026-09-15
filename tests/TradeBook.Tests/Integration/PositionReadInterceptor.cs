using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace TradeBook.Tests.Integration;

/// <summary>
/// Runs a callback every time the API finishes reading from the positions
/// table. Reading, not writing: the moment after a request has loaded a
/// position and before it tries to save is exactly the window a concurrent
/// change has to land in. Registered per test through
/// <see cref="TradeBookApiFactory"/>, so it only sees that test's requests.
/// </summary>
public sealed class PositionReadInterceptor(Func<Task> afterPositionRead) : DbCommandInterceptor
{
    private int _positionReads;

    /// <summary>How many times the position was read. One per attempt.</summary>
    public int PositionReads => Volatile.Read(ref _positionReads);

    public override async ValueTask<InterceptionResult> DataReaderClosingAsync(
        DbCommand command,
        DataReaderClosingEventData eventData,
        InterceptionResult result)
    {
        // Only the SELECTs. The SaveChanges batch mentions [positions] too,
        // but it starts with INSERT or UPDATE, not SELECT.
        if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal)
            && command.CommandText.Contains("FROM [positions]", StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _positionReads);
            await afterPositionRead();
        }

        return result;
    }
}
