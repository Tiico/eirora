using Eirora.Contracts;
using Eirora.Simulation.Saving;

namespace Eirora.CloudSaving.Tests;

/// <summary>
/// The server's cloud save in memory, with the rules of <c>CloudSaveEndpoints</c> and <c>CloudSaveService</c>: the
/// world part must load as one, an upload names the version it is based on and gets the next, an older key epoch
/// conflicts, and a new one locks the contribution part it replaces.
/// </summary>
internal sealed class FakeCloudSaveStore : ICloudSaveStore
{
    public CloudSave? Current { get; set; }

    public List<(int KeyEpoch, byte[] Contributions)> Locked { get; } = [];

    public bool Online { get; set; } = true;

    public bool SignedIn { get; set; } = true;

    public int Uploads { get; private set; }

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
        if (!Online)
        {
            return Task.FromResult(new StoreReply<CloudSaveVersion>(StoreStatus.Offline));
        }

        if (!SignedIn)
        {
            return Task.FromResult(new StoreReply<CloudSaveVersion>(StoreStatus.SignedOut));
        }

        Uploads++;
        if (upload.World is not { Length: > 0 } || upload.Contributions is not { Length: > 0 } || upload.BaseVersion < 0 || upload.KeyEpoch < 1)
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
        if (upload.BaseVersion != version || upload.KeyEpoch < epoch)
        {
            return Task.FromResult(new StoreReply<CloudSaveVersion>(StoreStatus.Conflict, new CloudSaveVersion(version, epoch)));
        }

        if (Current is { } replaced && upload.KeyEpoch > replaced.KeyEpoch)
        {
            Locked.Add((replaced.KeyEpoch, replaced.Contributions));
        }

        Current = new CloudSave(version + 1, upload.KeyEpoch, upload.World, upload.Contributions, DateTimeOffset.UtcNow);
        return Task.FromResult(new StoreReply<CloudSaveVersion>(StoreStatus.Ok, new CloudSaveVersion(version + 1, upload.KeyEpoch)));
    }
}

/// <summary>A device's keystore and state file in memory; a new phone is a new instance.</summary>
internal sealed class MemoryStorage : ICloudSyncStorage
{
    private readonly Dictionary<Guid, string> _keys = [];
    private string? _state;

    public ContributionKey? Key(Guid accountId) => _keys.TryGetValue(accountId, out var text) ? ContributionKey.TryParse(text) : null;

    public void KeepKey(Guid accountId, ContributionKey key) => _keys[accountId] = key.ToText();

    public void ForgetKey(Guid accountId) => _keys.Remove(accountId);

    public CloudSyncState? State(Guid accountId) => CloudSyncState.TryParse(_state) is { } state && state.AccountId == accountId ? state : null;

    public void KeepState(CloudSyncState state) => _state = state.ToText();

    public void ForgetState() => _state = null;

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
