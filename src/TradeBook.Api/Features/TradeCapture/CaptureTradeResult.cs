namespace TradeBook.Api.Features.TradeCapture;

/// <summary>
/// Everything the handler can come back with. A closed set of records rather
/// than exceptions: the controller switches over the cases and maps each to a
/// status code, and the compiler knows the full list because the base
/// constructor is private and every case is nested here.
/// </summary>
public abstract record CaptureTradeResult
{
    private CaptureTradeResult()
    {
    }

    /// <summary>The trade was booked and the position updated. 201.</summary>
    public sealed record Captured(CaptureTradeResponse Response) : CaptureTradeResult;

    /// <summary>A trade with this external reference already existed; nothing was written. 200.</summary>
    public sealed record AlreadyCaptured(CaptureTradeResponse Response) : CaptureTradeResult;

    /// <summary>The account or instrument id does not exist. 404.</summary>
    public sealed record NotFound(string Resource, int Id) : CaptureTradeResult;

    /// <summary>One or more payload rules failed. 400, every failing field named.</summary>
    public sealed record Invalid(IReadOnlyDictionary<string, string[]> Errors) : CaptureTradeResult;

    /// <summary>
    /// Another request changed the position on every attempt and the retries
    /// were exhausted (design.md section 6). Nothing was written. 409; the
    /// caller may resubmit.
    /// </summary>
    public sealed record Conflict : CaptureTradeResult;
}
