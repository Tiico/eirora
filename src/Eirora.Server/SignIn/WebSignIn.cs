using System.Text;
using System.Text.Encodings.Web;

namespace Eirora.Server.SignIn;

/// <summary>Sign-in on the web, section <c>SignIn:Web</c>: Apple's web flow for the Android app and the deletion page.</summary>
public sealed class WebSignInOptions
{
    /// <summary>Apple's Services ID; its return URLs are <c>/signin/apple/callback</c> and <c>/account/delete</c> on this host.</summary>
    public string AppleServicesId { get; set; } = "";

    /// <summary>Google's OAuth client of type Web; the deletion page signs in with it.</summary>
    public string GoogleClientId { get; set; } = "";

    /// <summary>The Android app that receives Apple's answer; no other app on the phone can.</summary>
    public string AndroidPackage { get; set; } = "com.eirora.game";
}

/// <summary>
/// <c>POST /signin/apple/callback</c>: where Apple posts the result of Sign in with Apple on Android (ADR-0008). Apple only
/// returns to an HTTPS address, so the server hands the answer to the app as an Android <c>intent:</c> URL that names
/// the app's package, which only that app can receive.
/// </summary>
/// <remarks>
/// The server neither checks nor keeps anything here: the app checks the state, and the ID token is verified when the
/// app signs in with it. Nothing is logged (NFR-10).
/// </remarks>
public static class AppleWebCallback
{
    public const string Path = "/signin/apple/callback";
    public const string AppScheme = "eirora-signin";

    private static readonly string[] s_forwarded = ["code", "id_token", "state", "error"];

    public static void MapAppleWebCallback(this IEndpointRouteBuilder app) =>
        app.MapPost(Path, HandleAsync).DisableAntiforgery();

    private static async Task<IResult> HandleAsync(HttpRequest request, Microsoft.Extensions.Options.IOptions<WebSignInOptions> options)
    {
        var form = request.HasFormContentType ? await request.ReadFormAsync(request.HttpContext.RequestAborted) : null;
        var query = new StringBuilder();
        foreach (var name in s_forwarded)
        {
            if (form is not null && form.TryGetValue(name, out var value) && value.ToString() is { Length: > 0 } text)
            {
                query.Append(query.Length == 0 ? '?' : '&').Append(name).Append('=').Append(UrlEncoder.Default.Encode(text));
            }
        }

        if (query.Length == 0)
        {
            query.Append("?error=invalid_response");
        }

        var package = options.Value.AndroidPackage;
        return Results.Redirect($"intent://apple{query}#Intent;scheme={AppScheme};package={package};end", permanent: false, preserveMethod: false);
    }
}
