using Eirora.Contracts;

namespace Eirora.CloudSaving.Tests;

/// <summary>
/// The two phones' sides of a key transfer (ADR-0008, TASK-023b) against a relay with the server's rules: the new
/// phone begins and polls, the old phone seals and delivers, the key is handed over once, and nothing readable
/// passes the server.
/// </summary>
public sealed class KeyTransferTests
{
    private static readonly Guid s_account = Guid.NewGuid();

    [Fact]
    public async Task TheKeyGoesFromTheOldPhoneToTheNewOne()
    {
        var relay = new FakeKeyTransferStore();
        var oldPhone = new MemoryStorage();
        var key = ContributionKey.Generate(3);
        oldPhone.KeepKey(s_account, key);
        var newPhone = new MemoryStorage();
        var receiver = new KeyTransferReceiver(relay, newPhone);
        var sender = new KeyTransferSender(relay, oldPhone);

        var begun = await receiver.BeginAsync();
        var waiting = await receiver.PollAsync(s_account);
        var sent = await sender.SendAsync(s_account, KeyTransferTicket.TryParse(receiver.Ticket!.ToText())!);
        var received = await receiver.PollAsync(s_account);

        Assert.Equal(KeyTransferOutcome.Waiting, begun);
        Assert.Equal(KeyTransferOutcome.Waiting, waiting);
        Assert.Equal(KeyDeliveryOutcome.Delivered, sent);
        Assert.Equal(KeyTransferOutcome.Received, received);
        Assert.Equal(key.ToText(), newPhone.Key(s_account)!.ToText());
        Assert.Null(receiver.Ticket);
        Assert.Empty(relay.Transfers);
        Assert.True(relay.Delivered.Single().AsSpan().IndexOf(Convert.FromBase64String(key.ToText().Split(' ')[1])) < 0, "the key never passes the relay in the clear");
    }

    [Fact]
    public async Task ATransferThatRanOutOrWasReplacedIsOverForBothPhones()
    {
        var relay = new FakeKeyTransferStore();
        var oldPhone = new MemoryStorage();
        oldPhone.KeepKey(s_account, ContributionKey.Generate(1));
        var receiver = new KeyTransferReceiver(relay, new MemoryStorage());
        await receiver.BeginAsync();
        var ticket = receiver.Ticket!;
        relay.Expire(ticket.Id);

        Assert.Equal(KeyDeliveryOutcome.Expired, await new KeyTransferSender(relay, oldPhone).SendAsync(s_account, ticket));
        Assert.Equal(KeyTransferOutcome.Expired, await receiver.PollAsync(s_account));
        Assert.Null(receiver.Ticket);
        Assert.Equal(KeyTransferOutcome.Expired, await receiver.PollAsync(s_account));
    }

    [Fact]
    public async Task AKeyIsDeliveredOnlyOnceAndOnlyFromAPhoneThatHasOne()
    {
        var relay = new FakeKeyTransferStore();
        var oldPhone = new MemoryStorage();
        oldPhone.KeepKey(s_account, ContributionKey.Generate(1));
        var receiver = new KeyTransferReceiver(relay, new MemoryStorage());
        await receiver.BeginAsync();
        var sender = new KeyTransferSender(relay, oldPhone);

        Assert.Equal(KeyDeliveryOutcome.NoKey, await new KeyTransferSender(relay, new MemoryStorage()).SendAsync(s_account, receiver.Ticket!));
        Assert.Equal(KeyDeliveryOutcome.Delivered, await sender.SendAsync(s_account, receiver.Ticket!));
        Assert.Equal(KeyDeliveryOutcome.AlreadyDelivered, await sender.SendAsync(s_account, receiver.Ticket!));
    }

    [Fact]
    public async Task AKeySealedForAnotherPhoneOrAccountIsNotKept()
    {
        var relay = new FakeKeyTransferStore();
        var oldPhone = new MemoryStorage();
        oldPhone.KeepKey(s_account, ContributionKey.Generate(1));
        var newPhone = new MemoryStorage();
        var receiver = new KeyTransferReceiver(relay, newPhone);
        await receiver.BeginAsync();
        var forAnotherPhone = new KeyTransferTicket(receiver.Ticket!.Id, TransferKeys.Generate().PublicKey);

        await new KeyTransferSender(relay, oldPhone).SendAsync(s_account, forAnotherPhone);

        Assert.Equal(KeyTransferOutcome.Unreadable, await receiver.PollAsync(s_account));
        Assert.Null(newPhone.Key(s_account));
        Assert.Null(receiver.Ticket);
    }

    [Fact]
    public async Task ABadTicketIsRefusedBeforeAnythingIsSent()
    {
        var relay = new FakeKeyTransferStore();
        var oldPhone = new MemoryStorage();
        oldPhone.KeepKey(s_account, ContributionKey.Generate(1));

        Assert.Equal(KeyDeliveryOutcome.BadTicket, await new KeyTransferSender(relay, oldPhone).SendAsync(s_account, new KeyTransferTicket("t", new byte[32])));
        Assert.Empty(relay.Delivered);
    }

    [Fact]
    public async Task CancellingEndsTheTransferOnTheServerToo()
    {
        var relay = new FakeKeyTransferStore();
        var receiver = new KeyTransferReceiver(relay, new MemoryStorage());
        await receiver.BeginAsync();

        await receiver.CancelAsync();

        Assert.Null(receiver.Ticket);
        Assert.Empty(relay.Transfers);
    }

    [Fact]
    public async Task OfflineTheTransferStaysOpen()
    {
        var relay = new FakeKeyTransferStore();
        var receiver = new KeyTransferReceiver(relay, new MemoryStorage());
        await receiver.BeginAsync();
        relay.Online = false;

        Assert.Equal(KeyTransferOutcome.Offline, await receiver.PollAsync(s_account));
        Assert.NotNull(receiver.Ticket);
        relay.Online = true;
        Assert.Equal(KeyTransferOutcome.Waiting, await receiver.PollAsync(s_account));
    }

    [Fact]
    public async Task ANewTransferReplacesThePendingOne()
    {
        var relay = new FakeKeyTransferStore();
        var receiver = new KeyTransferReceiver(relay, new MemoryStorage());
        await receiver.BeginAsync();
        var first = receiver.Ticket!;

        await receiver.BeginAsync();

        Assert.NotEqual(first.Id, receiver.Ticket!.Id);
        Assert.NotEqual(first.ReceiverPublicKey, receiver.Ticket.ReceiverPublicKey);
        Assert.Single(relay.Transfers);
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
