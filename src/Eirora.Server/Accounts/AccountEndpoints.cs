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
/// Linking, removing a login and deleting the account each need a fresh sign-in with one of the account's own logins, so
/// that a stolen token cannot take the account over (threat model; an unlocked phone is only weakly covered). Account
/// actions never check the rest period: they are administrative and allowed during rest (RST-07).
/// </remarks>
public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var account = app.MapGroup("/api/account").RequireAuthorization();
        account.MapGet("", GetAsync);
        account.MapPost("/logins", LinkAsync);
        account.MapPost("/logins/{provider}/removal", RemoveLoginAsync);
        account.MapPost("/deletion", DeleteAsync);
    }

    private static async Task<IResult> GetAsync(ClaimsPrincipal user, AccountService accounts, CancellationToken cancellationToken) =>
        await accounts.GetAsync(AccountId(user), cancellationToken) is { } info
            ? TypedResults.Ok(info)
            : TypedResults.Unauthorized();

    private static async Task<IResult> LinkAsync(
        ClaimsPrincipal user,
        LoginLink link,
        ExternalTokenValidator validator,
        AccountService accounts,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(validator, link.Confirmation, cancellationToken) is not { } confirmation
            || await ValidateAsync(validator, link.Login, cancellationToken) is not { } identity)
        {
            return InvalidSignIn();
        }

        return await accounts.LinkAsync(AccountId(user), confirmation, identity, cancellationToken) switch
        {
            LinkResult.Linked => TypedResults.NoContent(),
            LinkResult.NotThisAccount => NotThisAccount(),
            LinkResult.LinkedToAnotherAccount => Conflict("The login belongs to another account."),
            _ => Conflict("The account already has a login with this provider."),
        };
    }

    private static async Task<IResult> RemoveLoginAsync(
        ClaimsPrincipal user,
        string provider,
        ExternalLogin? confirmation,
        ExternalTokenValidator validator,
        AccountService accounts,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(validator, confirmation, cancellationToken) is not { } identity)
        {
            return InvalidSignIn();
        }

        return await accounts.RemoveLoginAsync(AccountId(user), identity, provider, cancellationToken) switch
        {
            RemoveLoginResult.Removed => TypedResults.NoContent(),
            RemoveLoginResult.NotThisAccount => NotThisAccount(),
            RemoveLoginResult.NotLinked => TypedResults.NotFound(),
            _ => Conflict("The last login can only be removed by deleting the account."),
        };
    }

    private static async Task<IResult> DeleteAsync(
        ClaimsPrincipal user,
        ExternalLogin confirmation,
        ExternalTokenValidator validator,
        AccountService accounts,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(validator, confirmation, cancellationToken) is not { } identity)
        {
            return InvalidSignIn();
        }

        return await accounts.DeleteAsync(AccountId(user), identity, confirmation.AuthorizationCode, cancellationToken) switch
        {
            DeleteResult.Deleted => TypedResults.NoContent(),
            DeleteResult.NotFound => TypedResults.Unauthorized(),
            DeleteResult.NotThisAccount => NotThisAccount(),
            _ => TypedResults.Problem(
                "An account with an Apple login is deleted by confirming with Apple, including the authorization code.",
                statusCode: StatusCodes.Status400BadRequest),
        };
    }

    /// <summary>The verified sign-in, or null when it is missing or does not verify.</summary>
    private static async Task<ExternalIdentity?> ValidateAsync(
        ExternalTokenValidator validator,
        ExternalLogin? login,
        CancellationToken cancellationToken) =>
        login is null ? null : await validator.ValidateAsync(login.Provider, login.IdToken, login.Nonce, cancellationToken);

    private static Guid AccountId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(Claims.Subject)!);

    private static ProblemHttpResult InvalidSignIn() =>
        TypedResults.Problem("The sign-in could not be verified.", statusCode: StatusCodes.Status400BadRequest);

    private static ProblemHttpResult NotThisAccount() =>
        TypedResults.Problem("Confirm with a login of this account.", statusCode: StatusCodes.Status403Forbidden);

    private static ProblemHttpResult Conflict(string detail) => TypedResults.Problem(detail, statusCode: StatusCodes.Status409Conflict);
}
