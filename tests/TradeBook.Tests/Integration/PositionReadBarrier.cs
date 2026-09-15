namespace TradeBook.Tests.Integration;

/// <summary>
/// Holds the first <paramref name="parties"/> callers until all of them have
/// arrived, then releases everyone at once. Anyone arriving later passes
/// straight through, so a retry that reloads the position is not held up.
/// </summary>
public sealed class PositionReadBarrier(int parties)
{
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrived;

    public Task ArriveAsync()
    {
        if (Interlocked.Increment(ref _arrived) >= parties)
        {
            _released.TrySetResult();
        }

        // A timeout so a request that dies before reaching the barrier fails
        // the test with a clear cause instead of hanging it.
        return _released.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }
}
