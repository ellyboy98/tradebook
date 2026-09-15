namespace TradeBook.Api.Features.ClientConfig;

/// <summary>
/// What the browser page needs before anyone has signed in: where to send the
/// user to log in and which public client to identify as. Nothing here is a
/// secret; a public OIDC client has none.
/// </summary>
public sealed record ClientConfigResponse(string Authority, string ClientId, string HubPath);
