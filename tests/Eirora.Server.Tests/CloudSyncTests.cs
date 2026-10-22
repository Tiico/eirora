using Eirora.CloudSaving;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Saving;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Eirora.Server.Tests;

/// <summary>
/// Two headless devices and the real server (TASK-023, ACC-01, ACC-03): the same world on a new phone, two phones that
/// played offline merged without double contributions, a new key epoch on a phone without the key, and a contribution
/// part the server could not have sealed never merged.
/// </summary>
public sealed class CloudSyncTests(ServerFixture fixture)
{
    private const long Hour = 3600;
    private static readonly TimeReading s_install = new(1_780_304_400, "Europe/Stockholm", 7200);

    private readonly ServerFixture _fixture = fixture;
    private readonly ServerFactory _server = fixture.Server;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheWorldOnPhoneAGoesOnWithTheSameFingerprintOnPhoneB()
    {
        var login = _server.Providers.SignIn(Contracts.LoginProviders.Google);
        var a = await DeviceAsync(login, PlayedSave());
        var b = await DeviceAsync(login);

        var uploaded = await a.Sync.ReconcileAsync(a.AccountId);
        a.Storage.CopyKeyTo(b.Storage, a.AccountId);
        var downloaded = await b.Sync.ReconcileAsync(b.AccountId);

        Assert.Equal(SyncOutcome.Synced, uploaded.Outcome);
        Assert.Equal((SyncOutcome.Synced, true), (downloaded.Outcome, downloaded.Downloaded));
        Assert.Equal(a.Fingerprint, b.Fingerprint);
        Assert.Equal(a.AccountId, b.AccountId);
    }

    [Fact]
    public async Task TwoPhonesThatPlayedOfflineMergeWithoutDoubleContributionsOrBuildings()
    {
        var login = _server.Providers.SignIn(Contracts.LoginProviders.Google);
        var a = await DeviceAsync(login, PlayedSave());
        var b = await DeviceAsync(login);
        await a.Sync.ReconcileAsync(a.AccountId);
        a.Storage.CopyKeyTo(b.Storage, a.AccountId);
        await b.Sync.ReconcileAsync(b.AccountId);
        int day = a.Save.World.GameDay;
        var aSteps = a.Save.Apply("a-steps", new RecordSelfCare(day, Habit.Steps, ContributionSource.Verified, 7_000));
        Advance(b.Save, Hour);
        var bSteps = b.Save.Apply("b-steps", new RecordSelfCare(day, Habit.Steps, ContributionSource.Verified, 7_000));
        var bSchedule = b.Save.Apply("b-schedule", new ChangeRestSchedule(new RestSchedule(22 * 60, 8 * 60)));

        var fromA = await a.Sync.UploadAsync(a.AccountId);
        var fromB = await b.Sync.UploadAsync(b.AccountId);
        var backToA = await a.Sync.UploadAsync(a.AccountId);

        Assert.True(aSteps.Accepted && bSteps.Accepted && bSchedule.Accepted);
        Assert.Equal((SyncOutcome.Synced, false), (fromA.Outcome, fromA.Merged));
        Assert.Equal((SyncOutcome.Synced, true), (fromB.Outcome, fromB.Merged));
        Assert.Equal((SyncOutcome.Synced, true), (backToA.Outcome, backToA.Merged));
        Assert.Equal(a.Fingerprint, b.Fingerprint);
        Assert.Equal(7_000, a.Save.World.RecordedSelfCare(day, Habit.Steps, ContributionSource.Verified));
        Assert.Equal(new RestSchedule(22 * 60, 8 * 60), a.Save.World.PendingRestSchedule);
        Assert.Equal(3L, (await a.Api.GetCloudSaveAsync(a.Token)).Save!.Version);
    }

    [Fact]
    public async Task APhoneWithoutTheKeyBeginsANewKeyEpochAndTheServerLocksTheOldPart()
    {
        var login = _server.Providers.SignIn(Contracts.LoginProviders.Google);
        var old = await DeviceAsync(login, PlayedSave());
        await old.Sync.ReconcileAsync(old.AccountId);
        var fresh = await DeviceAsync(login);

        var result = await fresh.Sync.ReconcileAsync(fresh.AccountId);
        var (_, save) = await fresh.Api.GetCloudSaveAsync(fresh.Token);
        var (_, locked) = await fresh.Api.GetLockedContributionsAsync(fresh.Token);

        Assert.Equal((SyncOutcome.Synced, true, true), (result.Outcome, result.Downloaded, result.KeyEpochBegan));
        Assert.Equal(old.Save.World.GameDay, fresh.Save.World.LastClosedGameDay);
        Assert.Equal((2L, 2), (save!.Version, save.KeyEpoch));
        Assert.Equal([1], locked!.Select(l => l.KeyEpoch));
        Assert.NotNull(ContributionCipher.Open(old.Storage.Key(old.AccountId)!, old.AccountId, 1, locked![0].Contributions));
    }

    // Whoever reaches the database can swap the contribution part, but cannot seal one the key opens: the phone never
    // merges it, and asks the player (threat model).
    [Fact]
    public async Task AContributionPartChangedInTheDatabaseIsNeverMerged()
    {
        var login = _server.Providers.SignIn(Contracts.LoginProviders.Google);
        var phone = await DeviceAsync(login, PlayedSave());
        await phone.Sync.ReconcileAsync(phone.AccountId);
        await using (var db = _fixture.CreateDbContext())
        {
            var row = await db.CloudSaves.SingleAsync(s => s.AccountId == phone.AccountId, Cancel);
            row.Contributions[^1] ^= 0x01;
            db.Entry(row).Property(r => r.Contributions).IsModified = true;
            await db.SaveChangesAsync(Cancel);
        }

        var result = await phone.Sync.ReconcileAsync(phone.AccountId);

        Assert.Equal(SyncOutcome.ChoiceNeeded, result.Outcome);
        Assert.True(result.CloudSaveUnreadable);
    }

    [Fact]
    public async Task ASignedOutPhoneChangesNothing()
    {
        var login = _server.Providers.SignIn(Contracts.LoginProviders.Google);
        var phone = await DeviceAsync(login, PlayedSave(), token: "not-a-token");

        Assert.Equal(SyncOutcome.SignedOut, (await phone.Sync.ReconcileAsync(phone.AccountId)).Outcome);
    }

    private async Task<HeadlessDevice> DeviceAsync(PlayerLogin login, Save? save = null, string? token = null)
    {
        var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(login.Provider, login.Subject));
        var (_, account) = await api.GetAccountAsync(tokens.Access);
        var http = _server.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        return new HeadlessDevice(api, http, account!.Id, token ?? tokens.Access, save);
    }

    private static void Advance(Save save, long seconds)
    {
        var now = save.World.Now;
        save.AdvanceTo(new TimeReading(now.UtcSeconds + seconds, now.ZoneId, now.OffsetSeconds));
    }

    private static Save PlayedSave()
    {
        var save = Save.Begin(s_install, new RestSchedule(23 * 60, 8 * 60));
        Advance(save, 2 * Hour);
        save.Apply("steps", new RecordSelfCare(save.World.GameDay, Habit.Steps, ContributionSource.Verified, 6_000));
        Advance(save, Hour);
        return save;
    }

    /// <summary>One device against the real server: its keystore, its world and its sync.</summary>
    private sealed class HeadlessDevice
    {
        public HeadlessDevice(Api api, HttpClient http, Guid accountId, string token, Save? save)
        {
            Api = api;
            AccountId = accountId;
            Token = token;
            World = new MemoryWorld(save);
            Sync = new CloudSync(new HttpCloudSaveStore(http, token), Storage, World);
        }

        public Api Api { get; }

        public Guid AccountId { get; }

        public string Token { get; }

        public MemoryStorage Storage { get; } = new();

        public MemoryWorld World { get; }

        public CloudSync Sync { get; }

        public Save Save => World.Current!;

        public string Fingerprint => Save.World.Fingerprint();
    }

    private sealed class MemoryStorage : ICloudSyncStorage
    {
        private readonly Dictionary<Guid, string> _keys = [];
        private string? _state;

        public ContributionKey? Key(Guid accountId) => _keys.TryGetValue(accountId, out var text) ? ContributionKey.TryParse(text) : null;

        public void KeepKey(Guid accountId, ContributionKey key) => _keys[accountId] = key.ToText();

        public void ForgetKey(Guid accountId) => _keys.Remove(accountId);

        public CloudSyncState? State(Guid accountId) => CloudSyncState.TryParse(_state) is { } state && state.AccountId == accountId ? state : null;

        public void KeepState(CloudSyncState state) => _state = state.ToText();

        public void ForgetState() => _state = null;

        public void CopyKeyTo(MemoryStorage other, Guid accountId) => other._keys[accountId] = _keys[accountId];
    }

    private sealed class MemoryWorld(Save? save) : ILocalWorld
    {
        public Save? Current { get; private set; } = save;

        public void Replace(Save save, ReplaceReason reason) => Current = save;

        public void KeepAside(Save save)
        {
        }
    }
}
