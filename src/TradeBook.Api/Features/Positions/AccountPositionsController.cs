using Microsoft.AspNetCore.Mvc;

namespace TradeBook.Api.Features.Positions;

[ApiController]
[Route("api/accounts/{accountId:int}/positions")]
public sealed class AccountPositionsController(GetPositionsHandler handler) : ControllerBase
{
    /// <summary>Current positions with valuation (design.md section 7).</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="includeFlat">Also return positions whose net quantity is zero. Default false.</param>
    /// <param name="cancellationToken">Bound to the request being aborted.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PositionValuationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        int accountId,
        [FromQuery] bool includeFlat = false,
        CancellationToken cancellationToken = default)
    {
        var positions = await handler.HandleAsync(accountId, includeFlat, cancellationToken);

        return positions is null
            ? Problem(
                title: "Unknown account",
                detail: $"No account with id {accountId} exists.",
                statusCode: StatusCodes.Status404NotFound)
            : Ok(positions);
    }
}
