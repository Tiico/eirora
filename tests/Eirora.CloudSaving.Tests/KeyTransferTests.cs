using Eirora.Contracts;

namespace Eirora.CloudSaving.Tests;

/// <summary>
/// The two phones' sides of a key transfer (ADR-0008, TASK-023b) against a relay with the server's rules: the new
/// phone begins and polls, the old phone seals and delivers, the key is handed over once and kept only once the cloud
/// save confirms it, and nothing the relay holds lets anyone plant a key.
/// </summary>
public sealed class KeyTransferTests
{
    private const long Hour = 3600;
    private static readonly Guid s_account = Guid.NewGuid();

    [Fact]
    public async Task TheKeyGoesFromTheOldPhoneToTheNewOne()
    {
        var (store, relay, oldPhone, newPhone) = TwoPhones();
        var receiver = new KeyTransferReceiver(relay, newPhone.Sync);
        var sender = new KeyTransferSender(relay, oldPhone.Storage);
        var key = oldPhone.Storage.Key(s_account)!;

        var begun = await receiver.BeginAsync();
        var waiting = await receiver.PollAsync(s_account);
        var sent = await sender.SendAsync(s_account, KeyTransferTicket.TryParse(receiver.Ticket!.ToText())!);
        var received = await receiver.PollAsync(s_account);
        var synced = await newPhone.Sync.ReconcileAsync(s_account);

        Assert.Equal((KeyTransferOutcome.Waiting, KeyTransferOutcome.Waiting, KeyDeliveryOutcome.Delivered, KeyTransferOutcome.Received), (begun, waiting, sent, received));
        Assert.Equal(key.ToText(), newPhone.Storage.Key(s_account)!.ToText());
        Assert.Null(receiver.Ticket);
        Assert.Empty(relay.Transfers);
        Assert.True(relay.Delivered.Single().AsSpan().IndexOf(Convert.FromBase64String(key.ToText().Split(' ')[1])) < 0, "the key never passes the relay in the clear");
        Assert.Equal((SyncOutcome.Synced, true, false), (synced.Outcome, synced.Downloaded, synced.KeyEpochBegan));
        Assert.Equal(oldPhone.Fingerprint, newPhone.Fingerprint);
        Assert.Equal(1, store.Current!.KeyEpoch);
    }

    // The relay holds the transfer's id and public key, and knows the account; that is not enough to plant a key.
    [Fact]
    public async Task TheRelayCannotPlantAKeyOnTheNewPhone()
    {
        var (_, relay, _, newPhone) = TwoPhones();
        var receiver = new KeyTransferReceiver(relay, newPhone.Sync);
        await receiver.BeginAsync();
        var (id, stored) = relay.Transfers.Single();
        var planted = ContributionKey.Generate(1);
        var whatTheRelayHas = new KeyTransferTicket(id, stored.PublicKey, KeyTransferTicket.NewSecret());

        await relay.DeliverAsync(id, KeyTransferCipher.Seal(planted, whatTheRelayHas, s_account));

        Assert.Equal(KeyTransferOutcome.Unreadable, await receiver.PollAsync(s_account));
        Assert.Null(newPhone.Storage.Key(s_account));
        Assert.Null(receiver.Ticket);
    }

    // A key that opens but is not the cloud save's, as from an old phone whose epoch has been superseded, is not kept.
    [Fact]
    public async Task AKeyOfAnOlderEpochIsNotKept()
    {
        var (store, relay, oldPhone, newPhone) = TwoPhones();
        var another = new Device(store);
        await another.Sync.ReconcileAsync(s_account, WorldChoice.TakeCloud); // Epoch 2 on the server; the old phone keeps epoch 1.
        var receiver = new KeyTransferReceiver(relay, newPhone.Sync);
        await receiver.BeginAsync();
        await new KeyTransferSender(relay, oldPhone.Storage).SendAsync(s_account, receiver.Ticket!);

        Assert.Equal(KeyTransferOutcome.Rejected, await receiver.PollAsync(s_account));
        Assert.Null(newPhone.Storage.Key(s_account));
        Assert.Equal(2, store.Current!.KeyEpoch);
    }

    [Fact]
    public async Task AKeyThatArrivedWhileOfflineIsKeptOnTheNextTry()
    {
        var (store, relay, oldPhone, newPhone) = TwoPhones();
        var receiver = new KeyTransferReceiver(relay, newPhone.Sync);
        await receiver.BeginAsync();
        await new KeyTransferSender(relay, oldPhone.Storage).SendAsync(s_account, receiver.Ticket!);
        store.Online = false;

        var offline = await receiver.PollAsync(s_account);
        store.Online = true;
        var kept = await receiver.PollAsync(s_account);

        Assert.Equal((KeyTransferOutcome.Offline, KeyTransferOutcome.Received), (offline, kept));
        Assert.Equal(oldPhone.Storage.Key(s_account)!.ToText(), newPhone.Storage.Key(s_account)!.ToText());
    }

    [Fact]
    public async Task ATransferThatRanOutOrWasReplacedIsOverForBothPhones()
    {
        var (_, relay, oldPhone, newPhone) = TwoPhones();
        var receiver = new KeyTransferReceiver(relay, newPhone.Sync);
        await receiver.BeginAsync();
        var ticket = receiver.Ticket!;
        relay.Expire(ticket.Id);

        Assert.Equal(KeyDeliveryOutcome.Expired, await new KeyTransferSender(relay, oldPhone.Storage).SendAsync(s_account, ticket));
        Assert.Equal(KeyTransferOutcome.Expired, await receiver.PollAsync(s_account));
        Assert.Null(receiver.Ticket);
        Assert.Equal(KeyTransferOutcome.Expired, await receiver.PollAsync(s_account));
    }

    [Fact]
    public async Task AKeyIsDeliveredOnlyOnceAndOnlyFromAPhoneThatHasOne()
    {
        var (_, relay, oldPhone, newPhone) = TwoPhones();
        var receiver = new KeyTransferReceiver(relay, newPhone.Sync);
        await receiver.BeginAsync();
        var sender = new KeyTransferSender(relay, oldPhone.Storage);

        Assert.Equal(KeyDeliveryOutcome.NoKey, await new KeyTransferSender(relay, new MemoryStorage()).SendAsync(s_account, receiver.Ticket!));
        Assert.Equal(KeyDeliveryOutcome.Delivered, await sender.SendAsync(s_account, receiver.Ticket!));
        Assert.Equal(KeyDeliveryOutcome.AlreadyDelivered, await sender.SendAsync(s_account, receiver.Ticket!));
    }

    [Fact]
    public async Task AKeySealedForAnotherPhoneIsNotKept()
    {
        var (_, relay, oldPhone, newPhone) = TwoPhones();
        var receiver = new KeyTransferReceiver(relay, newPhone.Sync);
        await receiver.BeginAsync();
        var forAnotherPhone = new KeyTransferTicket(receiver.Ticket!.Id, TransferKeys.Generate().PublicKey, receiver.Ticket.Secret);

        await new KeyTransferSender(relay, oldPhone.Storage).SendAsync(s_account, forAnotherPhone);

        Assert.Equal(KeyTransferOutcome.Unreadable, await receiver.PollAsync(s_account));
        Assert.Null(newPhone.Storage.Key(s_account));
        Assert.Null(receiver.Ticket);
    }

    [Fact]
    public async Task ABadTicketIsRefusedBeforeAnythingIsSent()
    {
        var (_, relay, oldPhone, _) = TwoPhones();

        Assert.Equal(KeyDeliveryOutcome.BadTicket, await new KeyTransferSender(relay, oldPhone.Storage).SendAsync(s_account, new KeyTransferTicket("t", new byte[32], KeyTransferTicket.NewSecret())));
        Assert.Empty(relay.Delivered);
    }

    [Fact]
    public async Task CancellingEndsTheTransferOnTheServerToo()
    {
        var (_, relay, _, newPhone) = TwoPhones();
        var receiver = new KeyTransferReceiver(relay, newPhone.Sync);
        await receiver.BeginAsync();

        await receiver.CancelAsync();

        Assert.Null(receiver.Ticket);
        Assert.Empty(relay.Transfers);
    }

    [Fact]
    public async Task OfflineTheTransferStaysOpen()
    {
        var (_, relay, _, newPhone) = TwoPhones();
        var receiver = new KeyTransferReceiver(relay, newPhone.Sync);
        await receiver.BeginAsync();
        relay.Online = false;

        Assert.Equal(KeyTransferOutcome.Offline, await receiver.PollAsync(s_account));
        Assert.NotNull(receiver.Ticket);
        relay.Online = true;
        Assert.Equal(KeyTransferOutcome.Waiting, await receiver.PollAsync(s_account));
    }

    [Fact]
    public async Task ANewTransferReplacesThePendingOneWithNewKeysAndANewSecret()
    {
        var (_, relay, _, newPhone) = TwoPhones();
        var receiver = new KeyTransferReceiver(relay, newPhone.Sync);
        await receiver.BeginAsync();
        var first = receiver.Ticket!;

        await receiver.BeginAsync();

        Assert.NotEqual(first.Id, receiver.Ticket!.Id);
        Assert.NotEqual(first.ReceiverPublicKey, receiver.Ticket.ReceiverPublicKey);
        Assert.NotEqual(first.Secret, receiver.Ticket.Secret);
        Assert.Single(relay.Transfers);
    }

    /// <summary>An old phone whose world is on the server under key epoch 1, a new phone without a world or a key, and the relay.</summary>
    private static (FakeCloudSaveStore Store, FakeKeyTransferStore Relay, Device OldPhone, Device NewPhone) TwoPhones()
    {
        var store = new FakeCloudSaveStore();
        var save = Simulation.Saving.Save.Begin(new Simulation.Time.TimeReading(1_780_304_400, "Europe/Stockholm", 7200), new Simulation.Time.RestSchedule(23 * 60, 8 * 60));
        save.AdvanceTo(new Simulation.Time.TimeReading(1_780_304_400 + Hour, "Europe/Stockholm", 7200));
        var oldPhone = new Device(store, save);
        oldPhone.Sync.ReconcileAsync(s_account).GetAwaiter().GetResult();
        return (store, new FakeKeyTransferStore(), oldPhone, new Device(store));
    }
}

/// <summary>The server's key transfer relay in memory, with the rules of <c>KeyTransferService</c>.</summary>
internal sealed class FakeKeyTransferStore : IKeyTransferStore
{
    private int _serial;

    public Dictionary<string, (byte[] PublicKey, byte[]? SealedKey, bool Expired)> Transfers { get; } = [];

    public List<byte[]> Delivered { get; } = [];

    public bool Online { get; set; } = true;

    public void Expire(string id) => Transfers[id] = (Transfers[id].PublicKey, Transfers[id].SealedKey, true);

    public Task<StoreReply<KeyTransfer>> CreateAsync(byte[] receiverPublicKey)
    {
        if (!Online)
        {
            return Task.FromResult(new StoreReply<KeyTransfer>(StoreStatus.Offline));
        }

        Transfers.Clear();
        string id = "transfer-" + (++_serial);
        Transfers[id] = (receiverPublicKey, null, false);
        return Task.FromResult(new StoreReply<KeyTransfer>(StoreStatus.Ok, new KeyTransfer(id, receiverPublicKey, null, DateTimeOffset.UtcNow + KeyTransfer.Lifetime)));
    }

    public Task<StoreReply<KeyTransfer>> TakeAsync(string id)
    {
        if (!Online)
        {
            return Task.FromResult(new StoreReply<KeyTransfer>(StoreStatus.Offline));
        }

        if (!Transfers.TryGetValue(id, out var transfer) || transfer.Expired)
        {
            return Task.FromResult(new StoreReply<KeyTransfer>(StoreStatus.NotFound));
        }

        if (transfer.SealedKey is not null)
        {
            Transfers.Remove(id);
        }

        return Task.FromResult(new StoreReply<KeyTransfer>(StoreStatus.Ok, new KeyTransfer(id, transfer.PublicKey, transfer.SealedKey, DateTimeOffset.UtcNow + KeyTransfer.Lifetime)));
    }

    public Task<StoreReply<KeyTransfer>> DeliverAsync(string id, byte[] sealedKey)
    {
        if (!Online)
        {
            return Task.FromResult(new StoreReply<KeyTransfer>(StoreStatus.Offline));
        }

        if (!Transfers.TryGetValue(id, out var transfer) || transfer.Expired)
        {
            return Task.FromResult(new StoreReply<KeyTransfer>(StoreStatus.NotFound));
        }

        if (transfer.SealedKey is not null)
        {
            return Task.FromResult(new StoreReply<KeyTransfer>(StoreStatus.Conflict));
        }

        Transfers[id] = (transfer.PublicKey, sealedKey, false);
        Delivered.Add(sealedKey);
        return Task.FromResult(new StoreReply<KeyTransfer>(StoreStatus.Ok));
    }

    public Task<StoreReply<KeyTransfer>> CancelAsync(string id)
    {
        if (!Online)
        {
            return Task.FromResult(new StoreReply<KeyTransfer>(StoreStatus.Offline));
        }

        return Task.FromResult(new StoreReply<KeyTransfer>(Transfers.Remove(id) ? StoreStatus.Ok : StoreStatus.NotFound));
    }
}
