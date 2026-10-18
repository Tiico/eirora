using System.Security.Claims;
using Eirora.Contracts;
using Eirora.Server.Accounts;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Eirora.Server.SignIn;

/// <summary>
/// <c>POST /connect/token</c>: signs in with an Apple or Google ID token (<see cref="SignInGrant"/>) and refreshes tokens.
/// </summary>
/// <remarks>
/// OpenIddict parses and validates the request and issues the tokens; this endpoint decides which account they are for.
/// The access token carries only the account id.
/// </remarks>
public static class TokenEndpoint
{
    public static void MapTokenEndpoint(this IEndpointRouteBuilder app) => app.MapPost("/connect/token", HandleAsync);

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        ExternalTokenValidator validator,
        AccountService accounts,
        CancellationToken cancellationToken)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenIddict server request is missing.");

        if (request.GrantType == SignInGrant.GrantType)
        {
            var identity = await validator.ValidateAsync(
                (string?)request[SignInGrant.ProviderParameter],
                (string?)request[SignInGrant.IdTokenParameter],
                (string?)request[SignInGrant.NonceParameter],
                cancellationToken);
            if (identity is null)
            {
                return Reject(Errors.InvalidGrant, "The sign-in could not be verified.");
            }

            var (accountId, created) = await accounts.SignInAsync(identity, cancellationToken);
            var properties = new AuthenticationProperties();
            properties.Parameters[SignInGrant.AccountCreatedParameter] = created;
            return Results.SignIn(CreatePrincipal(accountId, request.GetScopes()), properties, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsRefreshTokenGrantType())
        {
            var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            if (result.Principal?.GetClaim(Claims.Subject) is not { } subject
                || !Guid.TryParse(subject, out var accountId)
                || !await accounts.ExistsAsync(accountId, cancellationToken))
            {
                return Reject(Errors.InvalidGrant, "The account no longer exists.");
            }

            return Results.SignIn(CreatePrincipal(accountId, result.Principal.GetScopes()), properties: null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return Reject(Errors.UnsupportedGrantType, "The grant type is not supported.");
    }

    private static ClaimsPrincipal CreatePrincipal(Guid accountId, IEnumerable<string> requestedScopes)
    {
        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, accountId.ToString());
        identity.SetScopes(requestedScopes.Where(scope => scope == Scopes.OfflineAccess));
        identity.SetDestinations(static _ => [Destinations.AccessToken]);
        return new ClaimsPrincipal(identity);
    }

    private static IResult Reject(string error, string description) => Results.Forbid(
        new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
        }),
        [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
}
