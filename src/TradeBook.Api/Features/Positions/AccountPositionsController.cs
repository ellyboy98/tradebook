using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradeBook.Api.Infrastructure.Auth;
using TradeBook.Api.Infrastructure.Http;

namespace TradeBook.Api.Features.Positions;

[ApiController]
[Route("api/accounts/{accountId:int}/positions")]
[Authorize(Policy = Policies.TraderOrOps)]
public sealed class AccountPositionsController(GetPositionsHandler handler) : ControllerBase
{
    /// <summary>Current positions with valuation (design.md section 7).</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="includeFlat">Also return positions whose net quantity is zero. Default false.</param>
    /// <param name="cancellationToken">Bound to the request being aborted.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PositionValuationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        int accountId,
        [FromQuery] bool includeFlat = false,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(accountId, includeFlat, Caller.From(User), cancellationToken);

        return result switch
        {
            QueryResult<IReadOnlyList<PositionValuationResponse>>.Found found => Ok(found.Value),
            QueryResult<IReadOnlyList<PositionValuationResponse>>.Forbidden => this.AccessDenied(),
            QueryResult<IReadOnlyList<PositionValuationResponse>>.NotFound notFound => this.UnknownResource(notFound.Resource, notFound.Id),
            _ => throw new UnreachableException($"Unhandled result {result.GetType().Name}"),
        };
    }
}
