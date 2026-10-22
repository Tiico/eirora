using System;
using System.Threading.Tasks;

namespace Eirora.CloudSaving
{
    /// <summary>
    /// The old phone's side of a key transfer (ADR-0008, TASK-023b): seals the account's contribution key for the new
    /// phone named by its ticket and delivers it through the server, which cannot open it.
    /// </summary>
    public sealed class KeyTransferSender
    {
        private readonly IKeyTransferStore _store;
        private readonly ICloudSyncStorage _storage;

        public KeyTransferSender(IKeyTransferStore store, ICloudSyncStorage storage)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public async Task<KeyDeliveryOutcome> SendAsync(Guid accountId, KeyTransferTicket ticket)
        {
            if (ticket is null)
            {
                throw new ArgumentNullException(nameof(ticket));
            }

            if (_storage.Key(accountId) is not { } key)
            {
                return KeyDeliveryOutcome.NoKey;
            }

            byte[] sealedKey;
            try
            {
                sealedKey = KeyTransferCipher.Seal(key, ticket.ReceiverPublicKey, accountId, ticket.Id);
            }
            catch (ArgumentException)
            {
                return KeyDeliveryOutcome.BadTicket;
            }

            var reply = await _store.DeliverAsync(ticket.Id, sealedKey);
            return reply.Status switch
            {
                StoreStatus.Ok => KeyDeliveryOutcome.Delivered,
                StoreStatus.Conflict => KeyDeliveryOutcome.AlreadyDelivered,
                StoreStatus.NotFound => KeyDeliveryOutcome.Expired,
                StoreStatus.Offline => KeyDeliveryOutcome.Offline,
                StoreStatus.SignedOut => KeyDeliveryOutcome.SignedOut,
                _ => KeyDeliveryOutcome.Failed,
            };
        }
    }

    public enum KeyDeliveryOutcome
    {
        /// <summary>The sealed key is on the server, waiting for the new phone.</summary>
        Delivered,

        /// <summary>This phone has no contribution key for the account, so there is nothing to send.</summary>
        NoKey,

        /// <summary>The ticket's public key is not usable.</summary>
        BadTicket,

        /// <summary>A key was delivered to this transfer already.</summary>
        AlreadyDelivered,

        /// <summary>The transfer ran out, or belongs to another account; the new phone begins a new one.</summary>
        Expired,

        /// <summary>The server could not be reached.</summary>
        Offline,

        /// <summary>The device holds no sign-in, or the server no longer accepts it.</summary>
        SignedOut,

        /// <summary>The server answered with something unexpected.</summary>
        Failed,
    }
}
