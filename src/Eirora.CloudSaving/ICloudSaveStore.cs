using System.Threading.Tasks;
using Eirora.Contracts;

namespace Eirora.CloudSaving
{
    /// <summary>
    /// The server's cloud save API for the signed-in account (ADR-0008, <c>/api/account/cloud-save</c>), as the device
    /// reaches it: the Unity client over its account session, the headless tests over an HTTP client. Never throws for
    /// the network or the sign-in; those are statuses.
    /// </summary>
    public interface ICloudSaveStore
    {
        /// <summary><c>GET</c>: the account's cloud save, or <see cref="StoreStatus.NotFound"/> when it has none.</summary>
        Task<StoreReply<CloudSave>> GetAsync();

        /// <summary>
        /// <c>PUT</c>: the new version, or <see cref="StoreStatus.Conflict"/> with the current one when the upload was
        /// based on another version or an older key epoch, or its parts do not match the player's consent.
        /// </summary>
        Task<StoreReply<CloudSaveVersion>> UploadAsync(CloudSaveUpload upload);

        /// <summary><c>GET /api/account/contribution-consent</c>: the player's answer about the contribution part (TASK-060).</summary>
        Task<StoreReply<ContributionConsent>> GetConsentAsync();

        /// <summary>
        /// <c>PUT /api/account/contribution-consent</c>: keeps the player's answer; a decline deletes the contribution part
        /// and the locked ones on the server.
        /// </summary>
        Task<StoreReply<ContributionConsent>> AnswerConsentAsync(ContributionConsentAnswer answer);
    }

    public enum StoreStatus
    {
        /// <summary>The call succeeded and the reply holds its value.</summary>
        Ok,

        /// <summary>The account has no cloud save.</summary>
        NotFound,

        /// <summary>
        /// The upload was based on another version or an older key epoch, or did not match the player's consent; the reply
        /// holds the current version.
        /// </summary>
        Conflict,

        /// <summary>The server did not take the upload: wrong format or too large.</summary>
        Rejected,

        /// <summary>The server could not be reached.</summary>
        Offline,

        /// <summary>The device holds no sign-in, or the server no longer accepts it.</summary>
        SignedOut,

        /// <summary>The server answered with something else.</summary>
        Failed,
    }

    /// <summary>The server's answer: a status, and the value for the statuses that carry one.</summary>
    public readonly struct StoreReply<T>
        where T : class
    {
        public StoreReply(StoreStatus status, T? value = null)
        {
            Status = status;
            Value = value;
        }

        public StoreStatus Status { get; }

        public T? Value { get; }
    }
}
