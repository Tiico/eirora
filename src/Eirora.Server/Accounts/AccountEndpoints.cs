using System.Security.Claims;
using Eirora.Contracts;
using Eirora.Server.SignIn;
using Microsoft.AspNetCore.Http.HttpResults;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Eirora.Server.Accounts;

/// <summary>
/// <c>/api/account</c>: the signed-in player's own account. No route takes an account id, so a token only ever reaches
/// the account it was issued for (threat model: IDOR).
/// </summary>
/// <remarks>
/// Account actions never check the rest period: they are administrative and allowed during rest (RST-07).
/// </remarks>
public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var account = app.MapGroup("/api/account").RequireAuthorization();
        account.MapGet("", GetAsync);
        account.MapPost("/logins", LinkAsync);
        account.MapDelete("/logins/{provider}", UnlinkAsync);
        account.MapPost("/deletion", DeleteAsync);
    }

    private static async Task<IResult> GetAsync(ClaimsPrincipal user, AccountService accounts, CancellationToken cancellationToken) =>
        await accounts.GetAsync(AccountId(user), cancellationToken) is { } info
            ? TypedResults.Ok(info)
            : TypedResults.Unauthorized();

    private static async Task<IResult> LinkAsync(
        ClaimsPrincipal user,
        ExternalLogin login,
        ExternalTokenValidator validator,
        AccountService accounts,
        CancellationToken cancellationToken)
    {
        var identity = await validator.ValidateAsync(login.Provider, login.IdToken, login.Nonce, cancellationToken);
        if (identity is null)
        {
            return InvalidSignIn();
        }

        return await accounts.LinkAsync(AccountId(user), identity, cancellationToken) switch
        {
            LinkResult.Linked => TypedResults.NoContent(),
            LinkResult.LinkedToAnotherAccount => Conflict("The login belongs to another account."),
            _ => Conflict("The account already has a login with this provider."),
        };
    }

    private static async Task<IResult> UnlinkAsync(ClaimsPrincipal user, string provider, AccountService accounts, CancellationToken cancellationToken) =>
        await accounts.UnlinkAsync(AccountId(user), provider, cancellationToken) switch
        {
            UnlinkResult.Unlinked => TypedResults.NoContent(),
            UnlinkResult.NotLinked => TypedResults.NotFound(),
            _ => Conflict("The last login can only be removed by deleting the account."),
        };

    private static async Task<IResult> DeleteAsync(
        ClaimsPrincipal user,
        ExternalLogin confirmation,
        ExternalTokenValidator validator,
        AccountService accounts,
        CancellationToken cancellationToken)
    {
        var identity = await validator.ValidateAsync(confirmation.Provider, confirmation.IdToken, confirmation.Nonce, cancellationToken);
        if (identity is null)
        {
            return InvalidSignIn();
        }

        return await accounts.DeleteAsync(AccountId(user), identity, confirmation.AuthorizationCode, cancellationToken) switch
        {
            DeleteResult.Deleted => TypedResults.NoContent(),
            DeleteResult.NotFound => TypedResults.Unauthorized(),
            DeleteResult.NotThisAccount => TypedResults.Problem(
                "Confirm with a login of this account.", statusCode: StatusCodes.Status403Forbidden),
            _ => TypedResults.Problem(
                "An account with an Apple login is deleted by confirming with Apple, including the authorization code.",
                statusCode: StatusCodes.Status400BadRequest),
        };
    }

    private static Guid AccountId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(Claims.Subject)!);

    private static ProblemHttpResult InvalidSignIn() =>
        TypedResults.Problem("The sign-in could not be verified.", statusCode: StatusCodes.Status400BadRequest);

    private static ProblemHttpResult Conflict(string detail) => TypedResults.Problem(detail, statusCode: StatusCodes.Status409Conflict);
}
