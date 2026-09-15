using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace TradeBook.Api.Features.TradeCapture;

[ApiController]
[Route("api/trades")]
public sealed class TradesController(
    CaptureTradeHandler captureHandler,
    BlotterHandler blotterHandler) : ControllerBase
{
    /// <summary>Capture an execution (design.md section 7).</summary>
    [HttpPost]
    [ProducesResponseType<CaptureTradeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<CaptureTradeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Capture(CaptureTradeRequest request, CancellationToken cancellationToken)
    {
        // [ApiController] has already returned 400 for a body that does not
        // match the request shape. What reaches here is well-formed.

        // Until Keycloak arrives in build step 7 there is no authenticated
        // caller, so the audit column records a placeholder. Step 7 replaces
        // this with the token's sub claim and makes it mandatory.
        var capturedBySubject = User.FindFirstValue("sub") ?? "anonymous";

        var result = await captureHandler.HandleAsync(request, capturedBySubject, cancellationToken);

        return result switch
        {
            // No Location header: the contract has no GET /api/trades/{id}.
            CaptureTradeResult.Captured captured => Created((string?)null, captured.Response),
            CaptureTradeResult.AlreadyCaptured replay => Ok(replay.Response),
            CaptureTradeResult.NotFound notFound => Problem(
                title: $"Unknown {notFound.Resource}",
                detail: $"No {notFound.Resource} with id {notFound.Id} exists.",
                statusCode: StatusCodes.Status404NotFound),
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
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Blotter([FromQuery] BlotterQuery query, CancellationToken cancellationToken)
    {
        var page = await blotterHandler.HandleAsync(query, cancellationToken);

        return page is null
            ? Problem(
                title: "Unknown account",
                detail: $"No account with id {query.AccountId} exists.",
                statusCode: StatusCodes.Status404NotFound)
            : Ok(page);
    }
}
