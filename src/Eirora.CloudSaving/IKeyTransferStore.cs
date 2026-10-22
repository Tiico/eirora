using System.Threading.Tasks;
using Eirora.Contracts;

namespace Eirora.CloudSaving
{
    /// <summary>
    /// The server's key transfer API for the signed-in account (ADR-0008, <c>/api/account/key-transfers</c>), as the
    /// device reaches it. Never throws for the network or the sign-in; those are statuses.
    /// </summary>
    public interface IKeyTransferStore
    {
        /// <summary><c>POST</c>: a transfer with the new phone's one-time public key; <see cref="StoreStatus.Ok"/> with it.</summary>
        Task<StoreReply<KeyTransfer>> CreateAsync(byte[] receiverPublicKey);

        /// <summary>
        /// <c>GET</c>: the transfer, with the sealed key once delivered, which the server hands over once;
        /// <see cref="StoreStatus.NotFound"/> when it ran out, was taken or was replaced.
        /// </summary>
        Task<StoreReply<KeyTransfer>> TakeAsync(string id);

        /// <summary>
        /// <c>PUT</c>: the sealed key; <see cref="StoreStatus.Ok"/> without a value, <see cref="StoreStatus.Conflict"/>
        /// when one was delivered already, <see cref="StoreStatus.NotFound"/> when the transfer ran out.
        /// </summary>
        Task<StoreReply<KeyTransfer>> DeliverAsync(string id, byte[] sealedKey);

        /// <summary><c>DELETE</c>: the transfer, as when the new phone gives up.</summary>
        Task<StoreReply<KeyTransfer>> CancelAsync(string id);
    }
}
