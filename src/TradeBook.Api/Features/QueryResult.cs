namespace TradeBook.Api.Features;

/// <summary>
/// Outcome of an account-scoped read. Closed set: the base constructor is
/// private and every case is nested, so a controller switch covers them all.
/// </summary>
public abstract record QueryResult<T>
{
    private QueryResult()
    {
    }

    /// <summary>200 with the value.</summary>
    public sealed record Found(T Value) : QueryResult<T>;

    /// <summary>404. Only returned to callers entitled to know the resource is missing.</summary>
    public sealed record NotFound(string Resource, int Id) : QueryResult<T>;

    /// <summary>403. Says nothing about whether the account exists.</summary>
    public sealed record Forbidden : QueryResult<T>;
}
