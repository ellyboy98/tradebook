using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradeBook.Api.Infrastructure.Auth;

namespace TradeBook.Api.Features.Accounts;

[ApiController]
[Route("api/accounts")]
public sealed class AccountsController(
    ListAccountsHandler listHandler,
    CreateAccountHandler createHandler) : ControllerBase
{
    /// <summary>
    /// The caller's accounts: owned ones for a trader, all for ops. Not in the
    /// design's API table; added because the page's account rail needs it
    /// (ADR-015).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.TraderOrOps)]
    [ProducesResponseType<IReadOnlyList<AccountResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => Ok(await listHandler.HandleAsync(Caller.From(User), cancellationToken));

    /// <summary>Create an account (design.md section 7, ops only).</summary>
    [HttpPost]
    [Authorize(Policy = Policies.OpsOnly)]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var result = await createHandler.HandleAsync(request, cancellationToken);

        return result switch
        {
            CreateResult<AccountResponse>.Created created => Created((string?)null, created.Value),
            CreateResult<AccountResponse>.Conflict conflict => Problem(
                title: "Account already exists",
                detail: conflict.Detail,
                statusCode: StatusCodes.Status409Conflict),
            _ => throw new UnreachableException($"Unhandled result {result.GetType().Name}"),
        };
    }
}
