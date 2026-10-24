using System;
using System.Threading.Tasks;
using Eirora.CloudSaving;
using Eirora.Contracts;

namespace Eirora.Client.Accounts
{
    /// <summary>
    /// The cloud save API through the account session: every call carries a valid access token, refreshed when needed,
    /// and an unreachable server or a lost sign-in is a status, never an exception (ADR-0008).
    /// </summary>
    internal sealed class AccountCloudSaveStore : ICloudSaveStore
    {
        private readonly AccountSession _account;

        public AccountCloudSaveStore(AccountSession account) => _account = account;

        public async Task<StoreReply<CloudSave>> GetAsync()
        {
            CloudSave? save = null;
            var (outcome, reply) = await _account.AuthorizedAsync(async access =>
            {
                var (got, found) = await _account.Server.GetCloudSaveAsync(access);
                save = found;
                return got;
            });
            if (reply is null)
            {
                return new StoreReply<CloudSave>(Status(outcome));
            }

            return reply.Status switch
            {
                200 when save is not null => new StoreReply<CloudSave>(StoreStatus.Ok, save),
                404 => new StoreReply<CloudSave>(StoreStatus.NotFound),
                _ => new StoreReply<CloudSave>(StoreStatus.Failed),
            };
        }

        public async Task<StoreReply<CloudSaveVersion>> UploadAsync(CloudSaveUpload upload)
        {
            CloudSaveVersion? version = null;
            var (outcome, reply) = await _account.AuthorizedAsync(async access =>
            {
                var (sent, current) = await _account.Server.UploadCloudSaveAsync(access, upload);
                version = current;
                return sent;
            });
            if (reply is null)
            {
                return new StoreReply<CloudSaveVersion>(Status(outcome));
            }

            return reply.Status switch
            {
                200 when version is not null => new StoreReply<CloudSaveVersion>(StoreStatus.Ok, version),
                409 when version is not null => new StoreReply<CloudSaveVersion>(StoreStatus.Conflict, version),
                400 or 413 => new StoreReply<CloudSaveVersion>(StoreStatus.Rejected),
                _ => new StoreReply<CloudSaveVersion>(StoreStatus.Failed),
            };
        }

        public Task<StoreReply<ContributionConsent>> GetConsentAsync() =>
            ConsentAsync(access => _account.Server.GetConsentAsync(access));

        public Task<StoreReply<ContributionConsent>> AnswerConsentAsync(ContributionConsentAnswer answer) =>
            ConsentAsync(access => _account.Server.AnswerConsentAsync(access, answer));

        private async Task<StoreReply<ContributionConsent>> ConsentAsync(Func<string, Task<(HttpReply Reply, ContributionConsent? Consent)>> call)
        {
            ContributionConsent? consent = null;
            var (outcome, reply) = await _account.AuthorizedAsync(async access =>
            {
                var (sent, answer) = await call(access);
                consent = answer;
                return sent;
            });
            if (reply is null)
            {
                return new StoreReply<ContributionConsent>(Status(outcome));
            }

            return reply.Status == 200 && consent is not null
                ? new StoreReply<ContributionConsent>(StoreStatus.Ok, consent)
                : new StoreReply<ContributionConsent>(StoreStatus.Failed);
        }

        private static StoreStatus Status(AccountOutcome outcome) => outcome switch
        {
            AccountOutcome.Offline => StoreStatus.Offline,
            AccountOutcome.SignedOut => StoreStatus.SignedOut,
            _ => StoreStatus.Failed,
        };
    }
}
