using System.Text.Encodings.Web;
using Eirora.Contracts;
using Eirora.Server.SignIn;
using Microsoft.Extensions.Options;

namespace Eirora.Server.Accounts;

/// <summary>
/// <c>/account/delete</c>: the web page where a player deletes the account without the app, which Google Play requires
/// next to the deletion in the app (ADR-0008, ACC-05). The player confirms with a fresh sign-in with Apple or Google in
/// the browser; that sign-in is the only proof needed, as no other session exists on the web.
/// </summary>
/// <remarks>
/// The page's script is <c>wwwroot/account/delete.js</c>. It binds each sign-in to a nonce like the app does, and the
/// rules are the app's: an account with an Apple login is confirmed with Apple and its authorization code. With Sign in
/// with Apple off (<see cref="AppleSignIn"/>), the page offers Google only and loads nothing from Apple.
/// </remarks>
public static class DeletionPage
{
    public const string Path = "/account/delete";

    // Google Identity Services and, with Apple on, Sign in with Apple JS are the only scripts besides the page's own.
    private static readonly string s_contentSecurityPolicy = ContentSecurityPolicy(apple: false);
    private static readonly string s_contentSecurityPolicyWithApple = ContentSecurityPolicy(apple: true);

    public static void MapDeletionPage(this IEndpointRouteBuilder app)
    {
        app.MapGet(Path, Page);
        app.MapPost(Path, DeleteAsync).DisableAntiforgery();
    }

    private static IResult Page(HttpContext context, IOptions<WebSignInOptions> options)
    {
        var headers = context.Response.Headers;
        var apple = options.Value.AppleServicesId.Length > 0;
        headers.ContentSecurityPolicy = apple ? s_contentSecurityPolicyWithApple : s_contentSecurityPolicy;
        headers["Cross-Origin-Opener-Policy"] = "same-origin-allow-popups"; // The providers' sign-in popups answer by postMessage.
        headers["Referrer-Policy"] = "no-referrer";
        headers.XContentTypeOptions = "nosniff";
        headers.CacheControl = "no-store";
        return Results.Content(Html(options.Value, apple), "text/html; charset=utf-8");
    }

    private static string ContentSecurityPolicy(bool apple)
    {
        var (script, frame) = apple ? (" https://appleid.cdn-apple.com", " https://appleid.apple.com") : ("", "");
        return "default-src 'none'; " +
            $"script-src 'self' https://accounts.google.com/gsi/client{script}; " +
            "style-src 'self' 'unsafe-inline' https://accounts.google.com/gsi/style; " +
            $"img-src 'self' data:{script}; " +
            $"frame-src https://accounts.google.com/gsi/{frame}; " +
            $"connect-src 'self' https://accounts.google.com/gsi/{frame}; " +
            "base-uri 'none'; form-action 'none'; frame-ancestors 'none'";
    }

    private static async Task<IResult> DeleteAsync(
        ExternalLogin confirmation,
        HttpRequest request,
        IOptions<WebSignInOptions> web,
        ExternalTokenValidator validator,
        AccountService accounts,
        CancellationToken cancellationToken)
    {
        var identity = await validator.ValidateAsync(confirmation.Provider, confirmation.IdToken, confirmation.Nonce, cancellationToken);
        if (identity is null)
        {
            return TypedResults.Problem("The sign-in could not be verified.", statusCode: StatusCodes.Status400BadRequest);
        }

        var appleRedirectUri = web.Value.AppleRedirectUri(identity, request, Path);
        return await accounts.DeleteByLoginAsync(identity, confirmation.AuthorizationCode, appleRedirectUri, cancellationToken) switch
        {
            DeleteResult.Deleted => TypedResults.NoContent(),
            DeleteResult.AppleConfirmationRequired => TypedResults.Problem(
                "An account with an Apple login is deleted by confirming with Apple.", statusCode: StatusCodes.Status403Forbidden),
            _ => TypedResults.Problem("No account has this login.", statusCode: StatusCodes.Status404NotFound),
        };
    }

    private static string Html(WebSignInOptions options, bool apple)
    {
        var attribute = HtmlEncoder.Default;
        var appleServicesId = apple ? $" data-apple-services-id=\"{attribute.Encode(options.AppleServicesId)}\"" : "";
        var logins = apple
            ? "De kopplade inloggningarna med Apple och Google: vilken leverantör det är och det id leverantören gett dig för Eirora."
            : "Den kopplade inloggningen med Google: det id Google gett dig för Eirora.";
        var revocation = apple ? "\n<p>Har kontot en Apple-inloggning återkallar vi också appens åtkomst till ditt Apple-ID.</p>" : "";
        var confirmWith = apple
            ? "Logga in med samma Apple- eller Google-konto som i spelet. Har kontot en Apple-inloggning bekräftar du med Apple."
            : "Logga in med samma Google-konto som i spelet.";
        var appleButton = apple
            ? "\n<div id=\"apple\" class=\"provider\" hidden><div id=\"appleid-signin\" data-mode=\"center-align\" data-type=\"sign-in\" data-color=\"black\" data-border=\"false\" data-border-radius=\"50\" data-height=\"44\" data-width=\"260\"></div></div>"
            : "";
        var appleScript = apple
            ? "\n<script src=\"https://appleid.cdn-apple.com/appleauth/static/jsapi/appleid/1/sv_SE/appleid.auth.js\" async></script>"
            : "";
        return $$"""
            <!doctype html>
            <html lang="sv">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Radera ditt konto i Eirora</title>
            <link rel="stylesheet" href="/account/delete.css">
            </head>
            <body data-google-client-id="{{attribute.Encode(options.GoogleClientId)}}"{{appleServicesId}}>
            <main>
            <h1>Radera ditt konto i Eirora</h1>
            <p>Här raderar du ditt konto i spelet Eirora utan att öppna appen. I appen gör du det under <strong>Konto → Radera kontot</strong>.</p>

            <h2>Det här raderas</h2>
            <ul>
            <li>Kontot: dess id och när det skapades.</li>
            <li>{{logins}}</li>
            <li>Molnsparningen av din värld: kartan och världens läge, och dina bidrag, som är krypterade på telefonen så att servern aldrig kan läsa dem. Även äldre bidrag som sparats i väntan på en förlorad nyckel raderas.</li>
            <li>En pågående överföring av din nyckel mellan två telefoner, som servern bara förmedlar förseglad och som ändå raderas inom tio minuter.</li>
            <li>Alla inloggningar på dina enheter, så att ingen av dem når kontot längre.</li>
            </ul>
            <p>Det är allt servern sparar för ett konto. Inget namn och ingen e-post sparas.</p>
            <p>Inget sparas kvar om dig. Säkerhetskopiorna av databasen gallras efter 30 dagar, så inom 30 dagar är kontot borta även där. Serverns loggar innehåller inga uppgifter om dig.</p>{{revocation}}
            <p>Din värld på telefonen påverkas inte. Den kräver inget konto och finns kvar tills du tar bort appen.</p>

            <h2>Bekräfta att det är ditt konto</h2>
            <p>{{confirmWith}}</p>
            <div id="sign-in">{{appleButton}}
            <div id="google" class="provider" hidden></div>
            <p id="unavailable" hidden>Inloggning på webben är inte påslagen än. Radera kontot i appen under Konto → Radera kontot.</p>
            </div>

            <div id="confirm" hidden>
            <p id="signed-in"></p>
            <button id="delete" class="danger" type="button">Radera kontot för gott</button>
            <button id="cancel" type="button">Avbryt</button>
            </div>

            <p id="message" role="status" aria-live="polite"></p>
            </main>
            <script src="/account/delete.js"></script>{{appleScript}}
            <script src="https://accounts.google.com/gsi/client" async></script>
            </body>
            </html>
            """;
    }
}
