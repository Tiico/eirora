using System;
using System.Threading.Tasks;
using Eirora.Contracts;

namespace Eirora.CloudSaving
{
    /// <summary>
    /// The new phone's side of a key transfer (ADR-0008, TASK-023b): makes one-time keys and a transfer on the server,
    /// shows the ticket to the old phone, and polls until the sealed key arrives, which it opens and keeps. The private
    /// key lives only in this object, and a new transfer begins with new keys.
    /// </summary>
    public sealed class KeyTransferReceiver
    {
        private readonly IKeyTransferStore _store;
        private readonly ICloudSyncStorage _storage;
        private TransferKeys? _keys;

        public KeyTransferReceiver(IKeyTransferStore store, ICloudSyncStorage storage)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <summary>What the old phone needs, once <see cref="BeginAsync"/> succeeded: the ticket for the QR code.</summary>
        public KeyTransferTicket? Ticket { get; private set; }

        /// <summary>When the server forgets the transfer.</summary>
        public DateTimeOffset? ExpiresAt { get; private set; }

        /// <summary>Begins a transfer; on <see cref="KeyTransferOutcome.Waiting"/> the <see cref="Ticket"/> is ready.</summary>
        public async Task<KeyTransferOutcome> BeginAsync()
        {
            _keys = TransferKeys.Generate();
            Ticket = null;
            ExpiresAt = null;
            var reply = await _store.CreateAsync(_keys.PublicKey);
            if (reply.Status != StoreStatus.Ok || reply.Value is not { } transfer)
            {
                return Outcome(reply.Status);
            }

            Ticket = new KeyTransferTicket(transfer.Id, _keys.PublicKey);
            ExpiresAt = transfer.ExpiresAt;
            return KeyTransferOutcome.Waiting;
        }

        /// <summary>
        /// Asks the server once whether the old phone has delivered the key. On <see cref="KeyTransferOutcome.Received"/>
        /// the key is kept for <paramref name="accountId"/> and the transfer is over.
        /// </summary>
        public async Task<KeyTransferOutcome> PollAsync(Guid accountId)
        {
            if (Ticket is not { } ticket || _keys is not { } keys)
            {
                return KeyTransferOutcome.Expired;
            }

            var reply = await _store.TakeAsync(ticket.Id);
            if (reply.Status == StoreStatus.NotFound)
            {
                End();
                return KeyTransferOutcome.Expired;
            }

            if (reply.Status != StoreStatus.Ok || reply.Value is not { } transfer)
            {
                return Outcome(reply.Status);
            }

            if (transfer.SealedKey is null)
            {
                return KeyTransferOutcome.Waiting;
            }

            // The server handed the sealed key over once; whatever it holds, the transfer is over.
            End();
            var key = KeyTransferCipher.Open(keys, accountId, ticket.Id, transfer.SealedKey);
            if (key is null)
            {
                return KeyTransferOutcome.Unreadable;
            }

            _storage.KeepKey(accountId, key);
            return KeyTransferOutcome.Received;
        }

        /// <summary>Gives up the transfer, on the server too when it can be reached.</summary>
        public async Task CancelAsync()
        {
            if (Ticket is { } ticket)
            {
                End();
                await _store.CancelAsync(ticket.Id);
            }
        }

        private void End()
        {
            Ticket = null;
            ExpiresAt = null;
            _keys = null;
        }

        private static KeyTransferOutcome Outcome(StoreStatus status) => status switch
        {
            StoreStatus.Offline => KeyTransferOutcome.Offline,
            StoreStatus.SignedOut => KeyTransferOutcome.SignedOut,
            _ => KeyTransferOutcome.Failed,
        };
    }

    public enum KeyTransferOutcome
    {
        /// <summary>The transfer is open and the old phone has not delivered the key yet.</summary>
        Waiting,

        /// <summary>The key arrived, opened and is kept on this phone.</summary>
        Received,

        /// <summary>The transfer ran out, was taken or was replaced; a new one must begin.</summary>
        Expired,

        /// <summary>What arrived was not sealed for this phone in this transfer; a new one must begin.</summary>
        Unreadable,

        /// <summary>The server could not be reached; the transfer stays open until it runs out.</summary>
        Offline,

        /// <summary>The device holds no sign-in, or the server no longer accepts it.</summary>
        SignedOut,

        /// <summary>The server answered with something unexpected.</summary>
        Failed,
    }
}
