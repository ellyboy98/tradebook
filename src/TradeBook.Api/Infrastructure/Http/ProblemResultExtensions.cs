using Microsoft.AspNetCore.Mvc;

namespace TradeBook.Api.Infrastructure.Http;

/// <summary>
/// The two refusals every account-scoped endpoint shares, so they read the
/// same everywhere. Wording follows docs/ui-design.md section 7.
/// </summary>
public static class ProblemResultExtensions
{
    public const string AccessDeniedDetail = "You do not have access to this account.";

    public static ObjectResult AccessDenied(this ControllerBase controller) => controller.Problem(
        title: "Access denied",
        detail: AccessDeniedDetail,
        statusCode: StatusCodes.Status403Forbidden);

    public static ObjectResult UnknownResource(this ControllerBase controller, string resource, int id) => controller.Problem(
        title: $"Unknown {resource}",
        detail: $"No {resource} with id {id} exists.",
        statusCode: StatusCodes.Status404NotFound);
}
