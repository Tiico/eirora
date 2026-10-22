using System;
using System.Threading.Tasks;
using Eirora.Contracts;

namespace Eirora.CloudSaving
{
    /// <summary>
    /// The new phone's side of a key transfer (ADR-0008, TASK-023b): makes one-time keys, a secret and a transfer on
    /// the server, shows the ticket to the old phone, and polls until the sealed key arrives. A key that opens is kept
    /// only once the cloud save confirms it (<see cref="CloudSync.AcceptKeyAsync"/>): the epoch must be the cloud's,
    /// and the part must open with it. The private key and the secret live only in this object.
    /// </summary>
    public sealed class KeyTransferReceiver
    {
        private readonly IKeyTransferStore _store;
        private readonly CloudSync _sync;
        private TransferKeys? _keys;
        private ContributionKey? _unaccepted;

        public KeyTransferReceiver(IKeyTransferStore store, CloudSync sync)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _sync = sync ?? throw new ArgumentNullException(nameof(sync));
        }

        /// <summary>What the old phone needs, once <see cref="BeginAsync"/> succeeded: the ticket for the QR code.</summary>
        public KeyTransferTicket? Ticket { get; private set; }

        /// <summary>When the server forgets the transfer.</summary>
        public DateTimeOffset? ExpiresAt { get; private set; }

        /// <summary>Begins a transfer; on <see cref="KeyTransferOutcome.Waiting"/> the <see cref="Ticket"/> is ready.</summary>
        public async Task<KeyTransferOutcome> BeginAsync()
        {
            End();
            var keys = TransferKeys.Generate();
            var reply = await _store.CreateAsync(keys.PublicKey);
            if (reply.Status != StoreStatus.Ok || reply.Value is not { } transfer)
            {
                return Outcome(reply.Status);
            }

            _keys = keys;
            Ticket = new KeyTransferTicket(transfer.Id, keys.PublicKey, KeyTransferTicket.NewSecret());
            ExpiresAt = transfer.ExpiresAt;
            return KeyTransferOutcome.Waiting;
        }

        /// <summary>
        /// Asks the server once whether the old phone has delivered the key. On <see cref="KeyTransferOutcome.Received"/>
        /// the key is kept for <paramref name="accountId"/> and the transfer is over. A key that arrived but could not be
        /// checked against the cloud save (offline) is tried again on the next call.
        /// </summary>
        public async Task<KeyTransferOutcome> PollAsync(Guid accountId)
        {
            if (_unaccepted is { } pending)
            {
                return await AcceptAsync(accountId, pending);
            }

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
            var key = KeyTransferCipher.Open(keys, ticket, accountId, transfer.SealedKey);
            End();
            if (key is null)
            {
                return KeyTransferOutcome.Unreadable;
            }

            return await AcceptAsync(accountId, key);
        }

        /// <summary>Gives up the transfer, on the server too when it can be reached.</summary>
        public async Task CancelAsync()
        {
            var ticket = Ticket;
            End();
            _unaccepted = null;
            if (ticket is not null)
            {
                await _store.CancelAsync(ticket.Id);
            }
        }

        private async Task<KeyTransferOutcome> AcceptAsync(Guid accountId, ContributionKey key)
        {
            var acceptance = await _sync.AcceptKeyAsync(accountId, key);
            switch (acceptance)
            {
                case KeyAcceptance.Accepted:
                    _unaccepted = null;
                    return KeyTransferOutcome.Received;
                case KeyAcceptance.Offline:
                    _unaccepted = key;
                    return KeyTransferOutcome.Offline;
                case KeyAcceptance.SignedOut:
                    _unaccepted = key;
                    return KeyTransferOutcome.SignedOut;
                case KeyAcceptance.Failed:
                    _unaccepted = key;
                    return KeyTransferOutcome.Failed;
                default:
                    _unaccepted = null;
                    return KeyTransferOutcome.Rejected;
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

        /// <summary>The key arrived, opened, matched the cloud save and is kept on this phone.</summary>
        Received,

        /// <summary>The transfer ran out, was taken or was replaced; a new one must begin.</summary>
        Expired,

        /// <summary>What arrived was not sealed for this phone with this ticket; a new transfer must begin.</summary>
        Unreadable,

        /// <summary>The key arrived but is not the cloud save's: an older epoch, or it does not open the part. Not kept.</summary>
        Rejected,

        /// <summary>The server could not be reached; the transfer stays open until it runs out.</summary>
        Offline,

        /// <summary>The device holds no sign-in, or the server no longer accepts it.</summary>
        SignedOut,

        /// <summary>The server answered with something unexpected.</summary>
        Failed,
    }
}
