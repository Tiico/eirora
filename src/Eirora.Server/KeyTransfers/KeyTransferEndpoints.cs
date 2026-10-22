using System.Security.Claims;
using Eirora.Contracts;
using Eirora.Server.Accounts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Eirora.Server.KeyTransfers;

/// <summary>
/// <c>/api/account/key-transfers</c>: the signed-in account's key transfer between two of its phones (ADR-0008,
/// TASK-023b). No route takes an account id; both phones reach only the transfers of the account their token is for.
/// </summary>
public static class KeyTransferEndpoints
{
    private const long MaxBodyBytes = 8 * 1024;

    public static void MapKeyTransferEndpoints(this IEndpointRouteBuilder app)
    {
        var transfers = app.MapGroup("/api/account/key-transfers").RequireAuthorization();
        transfers.MapPost("", CreateAsync).WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes));
        transfers.MapGet("/{id}", TakeAsync);
        transfers.MapPut("/{id}", DeliverAsync).WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes));
        transfers.MapDelete("/{id}", DeleteAsync);
    }

    private static async Task<IResult> CreateAsync(ClaimsPrincipal user, KeyTransferRequest request, KeyTransferService transfers, CancellationToken cancellationToken)
    {
        if (request.ReceiverPublicKey is not { Length: KeyTransfer.PublicKeyLength } publicKey)
        {
            return TypedResults.Problem($"A transfer public key has {KeyTransfer.PublicKeyLength} bytes.", statusCode: StatusCodes.Status400BadRequest);
        }

        var transfer = await transfers.CreateAsync(AccountEndpoints.AccountId(user), publicKey, cancellationToken);
        return TypedResults.Created($"/api/account/key-transfers/{transfer.Id}", transfer);
    }

    private static async Task<IResult> TakeAsync(ClaimsPrincipal user, string id, KeyTransferService transfers, CancellationToken cancellationToken) =>
        await transfers.TakeAsync(AccountEndpoints.AccountId(user), id, cancellationToken) is { } transfer
            ? TypedResults.Ok(transfer)
            : TypedResults.NotFound();

    private static async Task<IResult> DeliverAsync(ClaimsPrincipal user, string id, KeyTransferDelivery delivery, KeyTransferService transfers, CancellationToken cancellationToken)
    {
        if (delivery.SealedKey is not { Length: > 0 and <= KeyTransfer.MaxSealedKeyBytes } sealedKey)
        {
            return TypedResults.Problem($"A sealed key has 1 to {KeyTransfer.MaxSealedKeyBytes} bytes.", statusCode: StatusCodes.Status400BadRequest);
        }

        return await transfers.DeliverAsync(AccountEndpoints.AccountId(user), id, sealedKey, cancellationToken) switch
        {
            DeliveryStatus.Delivered => TypedResults.NoContent(),
            DeliveryStatus.AlreadyDelivered => TypedResults.Conflict(),
            _ => TypedResults.NotFound(),
        };
    }

    private static async Task<IResult> DeleteAsync(ClaimsPrincipal user, string id, KeyTransferService transfers, CancellationToken cancellationToken) =>
        await transfers.DeleteAsync(AccountEndpoints.AccountId(user), id, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.NotFound();
}
