using System.Security.Claims;
using Eirora.Contracts;
using Eirora.Server.Accounts;
using Eirora.Simulation.Saving;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Eirora.Server.CloudSaves;

/// <summary>
/// <c>/api/account/cloud-save</c>: the signed-in player's cloud save (ADR-0008). Like the rest of <c>/api/account</c>, no
/// route takes an account id, so a token only ever reaches the cloud save of the account it was issued for.
/// </summary>
/// <remarks>
/// The server checks an upload's format and size, never its content: the world part must load as a save's world part,
/// which is as far as the server ever runs the own world (ADR-0001), and the contribution part is ciphertext it cannot read.
/// <c>/api/account/contribution-consent</c> holds the player's answer to whether the contribution part may be stored at
/// all (TASK-060), which every upload must match.
/// </remarks>
public static class CloudSaveEndpoints
{
    // Both parts at their largest, in base64 as JSON carries them, and room for the rest of the body.
    private const long MaxUploadBodyBytes = ((CloudSaveUpload.MaxWorldBytes + CloudSaveUpload.MaxContributionBytes) / 3 * 4) + (64 * 1024);

    public static void MapCloudSaveEndpoints(this IEndpointRouteBuilder app)
    {
        var cloudSave = app.MapGroup("/api/account/cloud-save").RequireAuthorization();
        cloudSave.MapGet("", GetAsync);
        cloudSave.MapPut("", UploadAsync).WithMetadata(new RequestSizeLimitAttribute(MaxUploadBodyBytes));
        cloudSave.MapGet("/locked", GetLockedAsync);
        cloudSave.MapDelete("/locked/{keyEpoch:int}", DeleteLockedAsync);

        var consent = app.MapGroup("/api/account/contribution-consent").RequireAuthorization();
        consent.MapGet("", GetConsentAsync);
        consent.MapPut("", AnswerConsentAsync);
    }

    private static async Task<IResult> GetAsync(ClaimsPrincipal user, CloudSaveService saves, CancellationToken cancellationToken) =>
        await saves.GetAsync(AccountEndpoints.AccountId(user), cancellationToken) is { } save
            ? TypedResults.Ok(save)
            : TypedResults.NotFound();

    private static async Task<IResult> UploadAsync(
        ClaimsPrincipal user,
        CloudSaveUpload upload,
        CloudSaveService saves,
        CancellationToken cancellationToken)
    {
        if (Rejection(upload) is { } rejection)
        {
            return rejection;
        }

        var result = await saves.UploadAsync(AccountEndpoints.AccountId(user), upload, cancellationToken);
        return result.Status switch
        {
            UploadStatus.Saved => TypedResults.Ok(result.Current),
            UploadStatus.Conflict => TypedResults.Conflict(result.Current),
            _ => TypedResults.Unauthorized(),
        };
    }

    private static async Task<IResult> GetLockedAsync(ClaimsPrincipal user, CloudSaveService saves, CancellationToken cancellationToken) =>
        TypedResults.Ok(await saves.GetLockedAsync(AccountEndpoints.AccountId(user), cancellationToken));

    private static async Task<IResult> DeleteLockedAsync(
        ClaimsPrincipal user,
        int keyEpoch,
        CloudSaveService saves,
        CancellationToken cancellationToken) =>
        await saves.DeleteLockedAsync(AccountEndpoints.AccountId(user), keyEpoch, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.NotFound();

    private static async Task<IResult> GetConsentAsync(ClaimsPrincipal user, CloudSaveService saves, CancellationToken cancellationToken) =>
        TypedResults.Ok(await saves.GetConsentAsync(AccountEndpoints.AccountId(user), cancellationToken));

    private static async Task<IResult> AnswerConsentAsync(
        ClaimsPrincipal user,
        ContributionConsentAnswer answer,
        CloudSaveService saves,
        CancellationToken cancellationToken)
    {
        // A consent counts only to a question the server knows the app asks.
        if (answer.Version < 1 || answer.Version > ContributionConsent.CurrentVersion)
        {
            return BadRequest($"The question's version is 1 to {ContributionConsent.CurrentVersion}.");
        }

        return await saves.AnswerConsentAsync(AccountEndpoints.AccountId(user), answer, cancellationToken) is { } consent
            ? TypedResults.Ok(consent)
            : TypedResults.Unauthorized();
    }

    /// <summary>Why the server does not take an upload, or null when its format and size are right.</summary>
    private static ProblemHttpResult? Rejection(CloudSaveUpload upload)
    {
        if (upload.World is not { Length: > 0 } world)
        {
            return BadRequest("An upload has a world part.");
        }

        if (upload.BaseVersion < 0 || (upload.HasContributions ? upload.KeyEpoch < 1 : upload.KeyEpoch != 0))
        {
            return BadRequest("Versions start at 0, and key epochs at 1 with a contribution part and 0 without.");
        }

        int contributions = upload.Contributions?.Length ?? 0;
        if (world.Length > CloudSaveUpload.MaxWorldBytes || contributions > CloudSaveUpload.MaxContributionBytes)
        {
            return TypedResults.Problem(
                $"A world part has at most {CloudSaveUpload.MaxWorldBytes} bytes and a contribution part at most {CloudSaveUpload.MaxContributionBytes}.",
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        try
        {
            Save.FromParts(world, null);
        }
        catch (SavedByNewerVersionException)
        {
            return BadRequest("The world part was saved by a newer version than the server's.");
        }
        catch (FormatException)
        {
            return BadRequest("The world part is not a save's world part.");
        }

        return null;
    }

    private static ProblemHttpResult BadRequest(string detail) => TypedResults.Problem(detail, statusCode: StatusCodes.Status400BadRequest);
}
