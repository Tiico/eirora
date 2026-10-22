namespace Eirora.Server.CloudSaves;

/// <summary>
/// The account's cloud save (ADR-0008): at most one per account, replaced as a whole by every upload and deleted with the
/// account.
/// </summary>
public sealed class StoredCloudSave
{
    public Guid AccountId { get; init; }

    /// <summary>1 for the first upload and one more for each upload after it; an upload must be based on it (D-99).</summary>
    public long Version { get; set; }

    public int KeyEpoch { get; set; }

    /// <summary>The save's world part, checked to load before it was stored.</summary>
    public required byte[] World { get; set; }

    /// <summary>The encrypted contribution part; the server never decrypts it (D-57).</summary>
    public required byte[] Contributions { get; set; }

    public DateTimeOffset SavedAt { get; set; }
}
