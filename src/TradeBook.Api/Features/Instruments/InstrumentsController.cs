using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradeBook.Api.Infrastructure.Auth;

namespace TradeBook.Api.Features.Instruments;

[ApiController]
[Route("api/instruments")]
public sealed class InstrumentsController(
    ListInstrumentsHandler listHandler,
    CreateInstrumentHandler createHandler) : ControllerBase
{
    /// <summary>Active instruments (design.md section 7).</summary>
    [HttpGet]
    [Authorize(Policy = Policies.TraderOrOps)]
    [ProducesResponseType<IReadOnlyList<InstrumentResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => Ok(await listHandler.HandleAsync(cancellationToken));

    /// <summary>Create an instrument (design.md section 7, ops only).</summary>
    [HttpPost]
    [Authorize(Policy = Policies.OpsOnly)]
    [ProducesResponseType<InstrumentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateInstrumentRequest request, CancellationToken cancellationToken)
    {
        var result = await createHandler.HandleAsync(request, cancellationToken);

        return result switch
        {
            CreateResult<InstrumentResponse>.Created created => Created((string?)null, created.Value),
            CreateResult<InstrumentResponse>.Conflict conflict => Problem(
                title: "Instrument already exists",
                detail: conflict.Detail,
                statusCode: StatusCodes.Status409Conflict),
            _ => throw new UnreachableException($"Unhandled result {result.GetType().Name}"),
        };
    }
}
