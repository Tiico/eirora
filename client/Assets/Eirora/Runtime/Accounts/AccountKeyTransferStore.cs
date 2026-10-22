using System.Threading.Tasks;
using Eirora.CloudSaving;
using Eirora.Contracts;

namespace Eirora.Client.Accounts
{
    /// <summary>The key transfer API through the account session (TASK-023b): a valid access token on every call, statuses for the rest.</summary>
    internal sealed class AccountKeyTransferStore : IKeyTransferStore
    {
        private readonly AccountSession _account;

        public AccountKeyTransferStore(AccountSession account) => _account = account;

        public Task<StoreReply<KeyTransfer>> CreateAsync(byte[] receiverPublicKey) =>
            WithTransferAsync(access => _account.Server.CreateKeyTransferAsync(access, receiverPublicKey), 201);

        public Task<StoreReply<KeyTransfer>> TakeAsync(string id) =>
            WithTransferAsync(access => _account.Server.GetKeyTransferAsync(access, id), 200);

        public async Task<StoreReply<KeyTransfer>> DeliverAsync(string id, byte[] sealedKey)
        {
            var (outcome, reply) = await _account.AuthorizedAsync(access => _account.Server.DeliverKeyTransferAsync(access, id, sealedKey));
            return reply is null ? new StoreReply<KeyTransfer>(Status(outcome)) : new StoreReply<KeyTransfer>(Status(reply.Status, 204));
        }

        public async Task<StoreReply<KeyTransfer>> CancelAsync(string id)
        {
            var (outcome, reply) = await _account.AuthorizedAsync(access => _account.Server.DeleteKeyTransferAsync(access, id));
            return reply is null ? new StoreReply<KeyTransfer>(Status(outcome)) : new StoreReply<KeyTransfer>(Status(reply.Status, 204));
        }

        private async Task<StoreReply<KeyTransfer>> WithTransferAsync(System.Func<string, Task<(HttpReply Reply, KeyTransfer? Transfer)>> call, int success)
        {
            KeyTransfer? transfer = null;
            var (outcome, reply) = await _account.AuthorizedAsync(async access =>
            {
                var (sent, found) = await call(access);
                transfer = found;
                return sent;
            });
            if (reply is null)
            {
                return new StoreReply<KeyTransfer>(Status(outcome));
            }

            return reply.Status == success
                ? transfer is null ? new StoreReply<KeyTransfer>(StoreStatus.Failed) : new StoreReply<KeyTransfer>(StoreStatus.Ok, transfer)
                : new StoreReply<KeyTransfer>(Status(reply.Status, success));
        }

        private static StoreStatus Status(int httpStatus, int success) => httpStatus switch
        {
            _ when httpStatus == success => StoreStatus.Ok,
            404 => StoreStatus.NotFound,
            409 => StoreStatus.Conflict,
            400 or 413 => StoreStatus.Rejected,
            _ => StoreStatus.Failed,
        };

        private static StoreStatus Status(AccountOutcome outcome) => outcome switch
        {
            AccountOutcome.Offline => StoreStatus.Offline,
            AccountOutcome.SignedOut => StoreStatus.SignedOut,
            _ => StoreStatus.Failed,
        };
    }
}
