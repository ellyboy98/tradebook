using Microsoft.AspNetCore.Mvc;
using TradeBook.Api.Infrastructure.Auth;

namespace TradeBook.Api.Infrastructure.Http;

/// <summary>
/// The two refusals every account-scoped endpoint shares, so they read the
/// same everywhere.
/// </summary>
public static class ProblemResultExtensions
{
    public static ObjectResult AccessDenied(this ControllerBase controller) => controller.Problem(
        title: "Access denied",
        detail: AccountAccess.DeniedMessage,
        statusCode: StatusCodes.Status403Forbidden);

    public static ObjectResult UnknownResource(this ControllerBase controller, string resource, int id) => controller.Problem(
        title: $"Unknown {resource}",
        detail: $"No {resource} with id {id} exists.",
        statusCode: StatusCodes.Status404NotFound);
}
