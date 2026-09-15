using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TradeBook.Api.Infrastructure.Auth;

namespace TradeBook.Api.Features.ClientConfig;

[ApiController]
[Route("api/client-config")]
public sealed class ClientConfigController(IOptions<KeycloakOptions> keycloak) : ControllerBase
{
    /// <summary>
    /// Anonymous by necessity: the page calls this to find out how to sign in.
    /// The authority is the public issuer URL, which is what the browser can
    /// reach, not the compose-internal one the API uses for metadata.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<ClientConfigResponse>(StatusCodes.Status200OK)]
    public ActionResult<ClientConfigResponse> Get()
        => new ClientConfigResponse(keycloak.Value.Issuer, keycloak.Value.WebClientId, "/hubs/positions");
}
