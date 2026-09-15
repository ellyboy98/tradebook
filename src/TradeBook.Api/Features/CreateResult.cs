namespace TradeBook.Api.Features;

/// <summary>
/// Outcome of creating a reference-data row. Request-shape validation has
/// already happened by the time a handler runs, so the only failure left is a
/// clash with something that already exists.
/// </summary>
public abstract record CreateResult<T>
{
    private CreateResult()
    {
    }

    /// <summary>201 with the new row.</summary>
    public sealed record Created(T Value) : CreateResult<T>;

    /// <summary>409. A row with the same natural key already exists.</summary>
    public sealed record Conflict(string Detail) : CreateResult<T>;
}
