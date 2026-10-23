using Eirora.Contracts;
using Eirora.Simulation.Saving;

namespace Eirora.CloudSaving.Tests;

/// <summary>
/// The server's cloud save in memory, with the rules of <c>CloudSaveEndpoints</c> and <c>CloudSaveService</c>: the
/// world part must load as one, an upload names the version it is based on and gets the next, an older key epoch
/// conflicts, and a new one locks the contribution part it replaces. An upload carries a contribution part exactly while
/// the player consents, and none goes through before they answered; the player here has consented unless a test says
/// otherwise (TASK-060).
/// </summary>
internal sealed class FakeCloudSaveStore : ICloudSaveStore
{
    public CloudSave? Current { get; set; }

    public List<(int KeyEpoch, byte[] Contributions)> Locked { get; } = [];

    public bool Online { get; set; } = true;

    /// <summary>False: the cloud save still comes down, but an upload does not get through, as on a connection that drops.</summary>
    public bool TakesUploads { get; set; } = true;

    public bool SignedIn { get; set; } = true;

    public int Uploads { get; private set; }

    /// <summary>How many uploads that carried a contribution part were stored.</summary>
    public int PartsStored { get; private set; }

    /// <summary>True: an answer about the consent gets a server error, as a 500 would.</summary>
    public bool FailsAnswers { get; set; }

    public ContributionConsent Consent { get; set; } = new(ConsentAnswer.Given, ContributionConsent.CurrentVersion, DateTimeOffset.UtcNow);

    public Task<StoreReply<ContributionConsent>> GetConsentAsync() =>
        Task.FromResult(!Online ? new StoreReply<ContributionConsent>(StoreStatus.Offline)
            : !SignedIn ? new StoreReply<ContributionConsent>(StoreStatus.SignedOut)
            : new StoreReply<ContributionConsent>(StoreStatus.Ok, Consent));

    public Task<StoreReply<ContributionConsent>> AnswerConsentAsync(ContributionConsentAnswer answer)
    {
        if (!Online)
        {
            return Task.FromResult(new StoreReply<ContributionConsent>(StoreStatus.Offline));
        }

        if (!SignedIn)
        {
            return Task.FromResult(new StoreReply<ContributionConsent>(StoreStatus.SignedOut));
        }

        if (FailsAnswers)
        {
            return Task.FromResult(new StoreReply<ContributionConsent>(StoreStatus.Failed));
        }

        Consent = new ContributionConsent(answer.Given ? ConsentAnswer.Given : ConsentAnswer.Declined, answer.Version, DateTimeOffset.UtcNow);
        if (!answer.Given)
        {
            Locked.Clear();
            if (Current is { } current)
            {
                Current = new CloudSave(current.Version, current.KeyEpoch, current.World, null, current.SavedAt);
            }
        }

        return Task.FromResult(new StoreReply<ContributionConsent>(StoreStatus.Ok, Consent));
    }

    public Task<StoreReply<CloudSave>> GetAsync()
    {
        if (!Online)
        {
            return Task.FromResult(new StoreReply<CloudSave>(StoreStatus.Offline));
        }

        if (!SignedIn)
        {
            return Task.FromResult(new StoreReply<CloudSave>(StoreStatus.SignedOut));
        }

        return Task.FromResult(Current is { } current ? new StoreReply<CloudSave>(StoreStatus.Ok, current) : new StoreReply<CloudSave>(StoreStatus.NotFound));
    }

    public Task<StoreReply<CloudSaveVersion>> UploadAsync(CloudSaveUpload upload)
    {
        if (!Online || !TakesUploads)
        {
            return Task.FromResult(new StoreReply<CloudSaveVersion>(StoreStatus.Offline));
        }

        if (!SignedIn)
        {
            return Task.FromResult(new StoreReply<CloudSaveVersion>(StoreStatus.SignedOut));
        }

        Uploads++;
        if (upload.World is not { Length: > 0 } || upload.BaseVersion < 0 || (upload.HasContributions ? upload.KeyEpoch < 1 : upload.KeyEpoch != 0))
        {
            return Task.FromResult(new StoreReply<CloudSaveVersion>(StoreStatus.Rejected));
        }

        try
        {
            Save.FromParts(upload.World, null);
        }
        catch (FormatException)
        {
            return Task.FromResult(new StoreReply<CloudSaveVersion>(StoreStatus.Rejected));
        }

        long version = Current?.Version ?? 0;
        int epoch = Current?.KeyEpoch ?? 0;
        bool given = Consent.Answer == ConsentAnswer.Given;
        if (Consent.Answer == ConsentAnswer.Unanswered || given != upload.HasContributions
            || upload.BaseVersion != version || (upload.HasContributions && upload.KeyEpoch < epoch))
        {
            return Task.FromResult(new StoreReply<CloudSaveVersion>(StoreStatus.Conflict, new CloudSaveVersion(version, epoch)));
        }

        if (Current is { Contributions: { } replaced } current && upload.HasContributions && upload.KeyEpoch > current.KeyEpoch)
        {
            Locked.Add((current.KeyEpoch, replaced));
        }

        int keyEpoch = Math.Max(epoch, upload.KeyEpoch);
        PartsStored += upload.HasContributions ? 1 : 0;
        Current = new CloudSave(version + 1, keyEpoch, upload.World, upload.HasContributions ? upload.Contributions : null, DateTimeOffset.UtcNow);
        return Task.FromResult(new StoreReply<CloudSaveVersion>(StoreStatus.Ok, new CloudSaveVersion(version + 1, keyEpoch)));
    }
}

/// <summary>A device's keystore and state file in memory; a new phone is a new instance.</summary>
internal sealed class MemoryStorage : ICloudSyncStorage
{
    private readonly Dictionary<Guid, string> _keys = [];
    private readonly HashSet<Guid> _declines = [];
    private string? _state;

    public ContributionKey? Key(Guid accountId) => _keys.TryGetValue(accountId, out var text) ? ContributionKey.TryParse(text) : null;

    public void KeepKey(Guid accountId, ContributionKey key) => _keys[accountId] = key.ToText();

    public void ForgetKey(Guid accountId) => _keys.Remove(accountId);

    public CloudSyncState? State(Guid accountId) => CloudSyncState.TryParse(_state) is { } state && state.AccountId == accountId ? state : null;

    public void KeepState(CloudSyncState state) => _state = state.ToText();

    public void ForgetState() => _state = null;

    public bool DeclineWaits(Guid accountId) => _declines.Contains(accountId);

    public void KeepDeclineWaiting(Guid accountId) => _declines.Add(accountId);

    public void ForgetDeclineWaiting(Guid accountId) => _declines.Remove(accountId);

    /// <summary>The key as the platform's key sync would carry it to another phone (TASK-023b).</summary>
    public void CopyKeyTo(MemoryStorage other, Guid accountId) => other._keys[accountId] = _keys[accountId];
}

/// <summary>A device's world in memory, remembering what was replaced and kept aside.</summary>
internal sealed class MemoryWorld : ILocalWorld
{
    public MemoryWorld(Save? save = null) => Current = save;

    public Save? Current { get; set; }

    public List<(Save Replaced, ReplaceReason Reason)> Replacements { get; } = [];

    public List<Save> Aside { get; } = [];

    public void Replace(Save save, ReplaceReason reason)
    {
        if (Current is { } replaced)
        {
            Replacements.Add((replaced, reason));
        }

        Current = save;
    }

    public void KeepAside(Save save) => Aside.Add(save);
}

/// <summary>One device: its storage, its world and its sync against a store.</summary>
internal sealed class Device
{
    public Device(ICloudSaveStore store, Save? save = null, MemoryStorage? storage = null)
    {
        Storage = storage ?? new MemoryStorage();
        World = new MemoryWorld(save);
        Sync = new CloudSync(store, Storage, World);
    }

    public MemoryStorage Storage { get; }

    public MemoryWorld World { get; }

    public CloudSync Sync { get; }

    public Save Save => World.Current ?? throw new InvalidOperationException("No world on the device.");

    public string Fingerprint => Save.World.Fingerprint();
}
