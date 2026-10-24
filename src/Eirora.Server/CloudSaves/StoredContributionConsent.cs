namespace Eirora.Server.CloudSaves;

/// <summary>
/// One answer to whether the contribution part may be stored on the account (TASK-060, GDPR Article 9(2)(a)). Every
/// answer is kept, so that the account shows what the player agreed to and when, also after a withdrawal; the latest is
/// the one in force. Deleted with the account.
/// </summary>
public sealed class StoredContributionConsent
{
    public long Id { get; init; }

    public Guid AccountId { get; init; }

    /// <summary>True when consent was given; false when it was declined or withdrawn.</summary>
    public bool Given { get; init; }

    /// <summary>The version of the question the player answered (<see cref="Contracts.ContributionConsent.CurrentVersion"/>).</summary>
    public int Version { get; init; }

    public DateTimeOffset AnsweredAt { get; init; }
}
