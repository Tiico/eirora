using System.Net;
using System.Security.Cryptography;
using Eirora.CloudSaving;
using Eirora.Contracts;
using Eirora.Server.KeyTransfers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Eirora.Server.Tests;

/// <summary>
/// The key transfer relay (ADR-0008, TASK-023b): the new phone creates a transfer, the old phone delivers the sealed
/// key, the new phone takes it once, and the server reads nothing, keeps nothing for long and lets no other account
/// near it.
/// </summary>
public sealed class KeyTransferTests(ServerFixture fixture)
{
    private readonly ServerFixture _fixture = fixture;
    private readonly ServerFactory _server = fixture.Server;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ATransferIsCreatedWithTheNewPhonesKeyAndWaitsForTheOldOne()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));
        var receiver = TransferKeys.Generate();
        var before = DateTimeOffset.UtcNow;

        var (status, transfer) = await api.CreateKeyTransferAsync(tokens.Access, new KeyTransferRequest(receiver.PublicKey));
        var (got, pending) = await api.GetKeyTransferAsync(tokens.Access, transfer!.Id);

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.Matches("^[A-Za-z0-9_-]{22}$", transfer.Id);
        Assert.Equal(receiver.PublicKey, transfer.ReceiverPublicKey);
        Assert.Null(transfer.SealedKey);
        Assert.InRange(transfer.ExpiresAt, before + KeyTransfer.Lifetime - TimeSpan.FromSeconds(5), DateTimeOffset.UtcNow + KeyTransfer.Lifetime + TimeSpan.FromSeconds(5));
        Assert.Equal(HttpStatusCode.OK, got);
        Assert.Null(pending!.SealedKey);
    }

    [Fact]
    public async Task TheOldPhoneDeliversTheSealedKeyAndTheNewPhoneTakesItOnce()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));
        var receiver = TransferKeys.Generate();
        var (_, transfer) = await api.CreateKeyTransferAsync(tokens.Access, new KeyTransferRequest(receiver.PublicKey));
        var accountId = (await api.GetAccountAsync(tokens.Access)).Account!.Id;
        var key = ContributionKey.Generate(1);
        byte[] sealedKey = KeyTransferCipher.Seal(key, transfer!.ReceiverPublicKey, accountId, transfer.Id);

        var delivered = await api.DeliverKeyTransferAsync(tokens.Access, transfer.Id, new KeyTransferDelivery(sealedKey));
        var (taken, received) = await api.GetKeyTransferAsync(tokens.Access, transfer.Id);
        var (again, _) = await api.GetKeyTransferAsync(tokens.Access, transfer.Id);

        Assert.Equal(HttpStatusCode.NoContent, delivered);
        Assert.Equal(HttpStatusCode.OK, taken);
        Assert.Equal(sealedKey, received!.SealedKey);
        Assert.Equal(key.ToText(), KeyTransferCipher.Open(receiver, accountId, transfer.Id, received.SealedKey!)!.ToText());
        Assert.Equal(HttpStatusCode.NotFound, again);
    }

    [Fact]
    public async Task ASealedKeyIsDeliveredOnlyOnceAndOnlyToAPendingTransfer()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));
        var (_, transfer) = await api.CreateKeyTransferAsync(tokens.Access, new KeyTransferRequest(TransferKeys.Generate().PublicKey));
        await api.DeliverKeyTransferAsync(tokens.Access, transfer!.Id, new KeyTransferDelivery(RandomNumberGenerator.GetBytes(128)));

        Assert.Equal(HttpStatusCode.Conflict, await api.DeliverKeyTransferAsync(tokens.Access, transfer.Id, new KeyTransferDelivery(RandomNumberGenerator.GetBytes(128))));
        Assert.Equal(HttpStatusCode.NotFound, await api.DeliverKeyTransferAsync(tokens.Access, "no-such-transfer", new KeyTransferDelivery(RandomNumberGenerator.GetBytes(128))));
    }

    [Fact]
    public async Task ANewTransferReplacesTheAccountsPendingOne()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));
        var (_, first) = await api.CreateKeyTransferAsync(tokens.Access, new KeyTransferRequest(TransferKeys.Generate().PublicKey));

        var (_, second) = await api.CreateKeyTransferAsync(tokens.Access, new KeyTransferRequest(TransferKeys.Generate().PublicKey));

        Assert.NotEqual(first!.Id, second!.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await api.GetKeyTransferAsync(tokens.Access, first.Id)).Status);
        Assert.Equal(HttpStatusCode.OK, (await api.GetKeyTransferAsync(tokens.Access, second.Id)).Status);
    }

    [Fact]
    public async Task ATransferCanBeCancelled()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));
        var (_, transfer) = await api.CreateKeyTransferAsync(tokens.Access, new KeyTransferRequest(TransferKeys.Generate().PublicKey));

        Assert.Equal(HttpStatusCode.NoContent, await api.DeleteKeyTransferAsync(tokens.Access, transfer!.Id));
        Assert.Equal(HttpStatusCode.NotFound, await api.DeleteKeyTransferAsync(tokens.Access, transfer.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await api.GetKeyTransferAsync(tokens.Access, transfer.Id)).Status);
    }

    [Fact]
    public async Task AnotherPlayersTokenNeverReachesTheTransfer()
    {
        using var api = _server.CreateApi();
        var owner = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));
        var other = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));
        var (_, transfer) = await api.CreateKeyTransferAsync(owner.Access, new KeyTransferRequest(TransferKeys.Generate().PublicKey));

        Assert.Equal(HttpStatusCode.NotFound, (await api.GetKeyTransferAsync(other.Access, transfer!.Id)).Status);
        Assert.Equal(HttpStatusCode.NotFound, await api.DeliverKeyTransferAsync(other.Access, transfer.Id, new KeyTransferDelivery(RandomNumberGenerator.GetBytes(128))));
        Assert.Equal(HttpStatusCode.NotFound, await api.DeleteKeyTransferAsync(other.Access, transfer.Id));
        Assert.Equal(HttpStatusCode.OK, (await api.GetKeyTransferAsync(owner.Access, transfer.Id)).Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task WithoutAValidTokenNoTransferIsReached(string accessToken)
    {
        using var api = _server.CreateApi();

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateKeyTransferAsync(accessToken, new KeyTransferRequest(TransferKeys.Generate().PublicKey))).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetKeyTransferAsync(accessToken, "x")).Status);
    }

    [Fact]
    public async Task AMalformedKeyOrAnOversizedSealedKeyIsRejected()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));

        Assert.Equal(HttpStatusCode.BadRequest, (await api.CreateKeyTransferAsync(tokens.Access, new KeyTransferRequest(new byte[31]))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.CreateKeyTransferAsync(tokens.Access, new { })).Status);
        var (_, transfer) = await api.CreateKeyTransferAsync(tokens.Access, new KeyTransferRequest(TransferKeys.Generate().PublicKey));
        Assert.Equal(HttpStatusCode.BadRequest, await api.DeliverKeyTransferAsync(tokens.Access, transfer!.Id, new KeyTransferDelivery(RandomNumberGenerator.GetBytes(KeyTransfer.MaxSealedKeyBytes + 1))));
        Assert.Equal(HttpStatusCode.BadRequest, await api.DeliverKeyTransferAsync(tokens.Access, transfer.Id, new KeyTransferDelivery([])));
        Assert.Equal(HttpStatusCode.NoContent, await api.DeliverKeyTransferAsync(tokens.Access, transfer.Id, new KeyTransferDelivery(RandomNumberGenerator.GetBytes(KeyTransfer.MaxSealedKeyBytes))));
    }

    // A transfer whose ten minutes ran out is as good as gone to both phones, and the pruning removes it.
    [Fact]
    public async Task ATransferRunsOutAfterTenMinutesAndIsPruned()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));
        var (_, transfer) = await api.CreateKeyTransferAsync(tokens.Access, new KeyTransferRequest(TransferKeys.Generate().PublicKey));
        var accountId = (await api.GetAccountAsync(tokens.Access)).Account!.Id;
        await using (var db = _fixture.CreateDbContext())
        {
            await db.KeyTransfers.Where(t => t.Id == transfer!.Id)
                .ExecuteUpdateAsync(t => t.SetProperty(x => x.ExpiresAt, DateTimeOffset.UtcNow.AddSeconds(-1)), Cancel);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await api.GetKeyTransferAsync(tokens.Access, transfer!.Id)).Status);
        Assert.Equal(HttpStatusCode.NotFound, await api.DeliverKeyTransferAsync(tokens.Access, transfer.Id, new KeyTransferDelivery(RandomNumberGenerator.GetBytes(64))));
        await using (var scope = _server.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Data.EiroraDbContext>();
            Assert.True(await db.KeyTransfers.AnyAsync(t => t.Id == transfer.Id, Cancel));
            await scope.ServiceProvider.GetRequiredService<KeyTransferService>().PruneAsync(Cancel);
            Assert.False(await db.KeyTransfers.AnyAsync(t => t.Id == transfer.Id, Cancel));
        }
    }

    [Fact]
    public async Task TheServerStoresOnlyWhatItCannotRead()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));
        var receiver = TransferKeys.Generate();
        var (_, transfer) = await api.CreateKeyTransferAsync(tokens.Access, new KeyTransferRequest(receiver.PublicKey));
        var accountId = (await api.GetAccountAsync(tokens.Access)).Account!.Id;
        var key = ContributionKey.Generate(1);
        byte[] sealedKey = KeyTransferCipher.Seal(key, transfer!.ReceiverPublicKey, accountId, transfer.Id);
        await api.DeliverKeyTransferAsync(tokens.Access, transfer.Id, new KeyTransferDelivery(sealedKey));

        await using var db = _fixture.CreateDbContext();
        var row = await db.KeyTransfers.SingleAsync(t => t.Id == transfer.Id, Cancel);

        Assert.Equal(sealedKey, row.SealedKey);
        Assert.DoesNotContain(Convert.FromBase64String(key.ToText().Split(' ')[1]), Chunks(row.SealedKey!, 8));
        Assert.Null(KeyTransferCipher.Open(TransferKeys.Generate(), accountId, transfer.Id, row.SealedKey!));
    }

    private static IEnumerable<byte[]> Chunks(byte[] bytes, int length) =>
        Enumerable.Range(0, bytes.Length - length + 1).Select(i => bytes.Skip(i).Take(length).ToArray());
}
