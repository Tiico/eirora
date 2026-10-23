using System;

namespace Eirora.Contracts
{
    /// <summary>
    /// Response of <c>GET</c> and <c>PUT /api/account/contribution-consent</c>: the player's latest answer to whether the
    /// contribution part may be stored on the account (TASK-060). The contribution part is data concerning health even
    /// encrypted, so the server stores it only with explicit consent (GDPR Article 9(2)(a)); the world part is stored
    /// whatever the answer.
    /// </summary>
    /// <remarks>
    /// The server takes no upload before the player has answered, a contribution part only while consent is given, and
    /// none without a contribution part while it is: an upload that does not match the answer gets 409, so that a device
    /// that missed a change asks again. Declining or withdrawing deletes the contribution part and the locked ones.
    /// </remarks>
    public sealed class ContributionConsent
    {
        /// <summary>
        /// The version of the question the app asks now. It goes up when what the player agrees to changes, and a consent
        /// given to an earlier version is asked again; a decline never is.
        /// </summary>
        public const int CurrentVersion = 1;

        public ContributionConsent(ConsentAnswer answer, int version, DateTimeOffset? answeredAt)
        {
            Answer = answer;
            Version = version;
            AnsweredAt = answeredAt;
        }

        public ConsentAnswer Answer { get; }

        /// <summary>The version of the question the player answered; 0 while unanswered.</summary>
        public int Version { get; }

        /// <summary>When the server stored the answer; null while unanswered.</summary>
        public DateTimeOffset? AnsweredAt { get; }

        /// <summary>Whether the app asks: before the first answer, and when consent was given to an earlier question.</summary>
        public bool NeedsAnswer => Answer == ConsentAnswer.Unanswered || (Answer == ConsentAnswer.Given && Version < CurrentVersion);
    }

    /// <summary>The player's answer to the question about the contribution part.</summary>
    public enum ConsentAnswer
    {
        /// <summary>Not asked yet, or the answer has not reached the server.</summary>
        Unanswered = 0,

        /// <summary>The contribution part is stored on the account.</summary>
        Given = 1,

        /// <summary>Declined, or withdrawn after it was given: only the world part is stored.</summary>
        Declined = 2,
    }

    /// <summary>
    /// Body of <c>PUT /api/account/contribution-consent</c>: the player's answer to the question of
    /// <see cref="Version"/>, which the server keeps with the time it arrived.
    /// </summary>
    public sealed class ContributionConsentAnswer
    {
        public ContributionConsentAnswer(bool given, int version)
        {
            Given = given;
            Version = version;
        }

        /// <summary>True to give consent; false to decline it, or to withdraw it.</summary>
        public bool Given { get; }

        /// <summary>The version of the question the player was shown: 1 to <see cref="ContributionConsent.CurrentVersion"/>.</summary>
        public int Version { get; }
    }
}
