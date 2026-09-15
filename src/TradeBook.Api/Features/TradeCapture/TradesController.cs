using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradeBook.Api.Infrastructure.Auth;
using TradeBook.Api.Infrastructure.Http;

namespace TradeBook.Api.Features.TradeCapture;

[ApiController]
[Route("api/trades")]
[Authorize(Policy = Policies.TraderOrOps)]
public sealed class TradesController(
    CaptureTradeHandler captureHandler,
    BlotterHandler blotterHandler) : ControllerBase
{
    /// <summary>Capture an execution (design.md section 7).</summary>
    [HttpPost]
    [ProducesResponseType<CaptureTradeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<CaptureTradeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Capture(CaptureTradeRequest request, CancellationToken cancellationToken)
    {
        // [ApiController] has already returned 400 for a body that does not
        // match the request shape. What reaches here is well-formed, and
        // [Authorize] has already rejected anonymous or role-less callers.
        var result = await captureHandler.HandleAsync(request, Caller.From(User), cancellationToken);

        return result switch
        {
            // No Location header: the contract has no GET /api/trades/{id}.
            CaptureTradeResult.Captured captured => Created((string?)null, captured.Response),
            CaptureTradeResult.AlreadyCaptured replay => Ok(replay.Response),
            CaptureTradeResult.Forbidden => this.AccessDenied(),
            CaptureTradeResult.NotFound notFound => this.UnknownResource(notFound.Resource, notFound.Id),
            CaptureTradeResult.Invalid invalid => ValidationProblem(
                new ValidationProblemDetails(invalid.Errors.ToDictionary(e => e.Key, e => e.Value))),
            // Wording from docs/ui-design.md section 7.
            CaptureTradeResult.Conflict => Problem(
                title: "Position changed",
                detail: "The position changed while this was submitted. Book it again.",
                statusCode: StatusCodes.Status409Conflict),
            _ => throw new UnreachableException($"Unhandled result {result.GetType().Name}"),
        };
    }

    /// <summary>Blotter: an account's executions, newest first (design.md section 7).</summary>
    [HttpGet]
    [ProducesResponseType<BlotterPage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Blotter([FromQuery] BlotterQuery query, CancellationToken cancellationToken)
    {
        var result = await blotterHandler.HandleAsync(query, Caller.From(User), cancellationToken);

        return result switch
        {
            QueryResult<BlotterPage>.Found found => Ok(found.Value),
            QueryResult<BlotterPage>.Forbidden => this.AccessDenied(),
            QueryResult<BlotterPage>.NotFound notFound => this.UnknownResource(notFound.Resource, notFound.Id),
            _ => throw new UnreachableException($"Unhandled result {result.GetType().Name}"),
        };
    }
}
