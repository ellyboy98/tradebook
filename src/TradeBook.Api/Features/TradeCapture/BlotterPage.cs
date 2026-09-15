namespace TradeBook.Api.Features.TradeCapture;

/// <summary>One page of the blotter, newest execution first.</summary>
public sealed record BlotterPage(
    IReadOnlyList<TradeResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
