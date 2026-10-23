using System.Text;
using FsCheck.Xunit;

namespace Eirora.CloudSaving.Tests;

/// <summary>
/// The contribution key sealed for one other phone (ADR-0008, TASK-023b): only the phone with the private half of the
/// ticket's key and the ticket's secret opens it, and only in the transfer it was sealed for. The server, which relays
/// it and stores the public key, cannot open it and cannot seal one the phone would open.
/// </summary>
public sealed class KeyTransferCipherTests
{
    private static readonly Guid s_account = Guid.Parse("019a0a4e-6b5c-7d2e-9f10-2a3b4c5d6e7f");

    [Property(MaxTest = 20)]
    public bool TheKeyOpensOnTheReceivingPhone(int epochSeed)
    {
        int epoch = 1 + Math.Abs(epochSeed % 1000);
        var key = ContributionKey.Generate(epoch);
        var (receiver, ticket) = NewPhone("t1");

        byte[] sealedKey = KeyTransferCipher.Seal(key, ticket, s_account);
        var opened = KeyTransferCipher.Open(receiver, ticket, s_account, sealedKey);

        return opened is not null && opened.Epoch == epoch && opened.ToText() == key.ToText();
    }

    [Fact]
    public void TheSealedKeyIsAHeaderTheSendersKeyANonceAndTheCiphertext()
    {
        var key = ContributionKey.Generate(1);
        var (_, ticket) = NewPhone("t1");

        byte[] sealedKey = KeyTransferCipher.Seal(key, ticket, s_account);

        Assert.Equal("eirora-transfer 1\n", Encoding.UTF8.GetString(sealedKey, 0, 18));
        Assert.Equal(18 + 32 + 24 + key.ToText().Length + 16, sealedKey.Length);
        Assert.DoesNotContain(key.ToText().Substring(2, 20), Encoding.Latin1.GetString(sealedKey), StringComparison.Ordinal);
    }

    [Fact]
    public void EverySealUsesItsOwnSenderKeyAndNonce()
    {
        var key = ContributionKey.Generate(1);
        var (_, ticket) = NewPhone("t1");

        byte[] first = KeyTransferCipher.Seal(key, ticket, s_account);
        byte[] second = KeyTransferCipher.Seal(key, ticket, s_account);

        Assert.NotEqual(first.Skip(18).Take(32), second.Skip(18).Take(32));
        Assert.NotEqual(first.Skip(50).Take(24), second.Skip(50).Take(24));
    }

    // What the server stores (the id and the public key) and knows (the account) is not enough to seal a key the phone
    // opens: the ticket's secret never reaches it.
    [Fact]
    public void WithoutTheTicketsSecretNoOneCanSealAKeyThePhoneOpens()
    {
        var (receiver, ticket) = NewPhone("t1");
        var planted = ContributionKey.Generate(7);
        var whatTheServerHas = new KeyTransferTicket(ticket.Id, ticket.ReceiverPublicKey, KeyTransferTicket.NewSecret());

        byte[] sealedByTheServer = KeyTransferCipher.Seal(planted, whatTheServerHas, s_account);

        Assert.Null(KeyTransferCipher.Open(receiver, ticket, s_account, sealedByTheServer));
    }

    [Fact]
    public void AnotherPhoneAccountOrTransferDoesNotOpenIt()
    {
        var key = ContributionKey.Generate(1);
        var (receiver, ticket) = NewPhone("t1");
        byte[] sealedKey = KeyTransferCipher.Seal(key, ticket, s_account);
        var otherTransfer = new KeyTransferTicket("t2", ticket.ReceiverPublicKey, ticket.Secret);

        Assert.NotNull(KeyTransferCipher.Open(receiver, ticket, s_account, sealedKey));
        Assert.Null(KeyTransferCipher.Open(TransferKeys.Generate(), ticket, s_account, sealedKey));
        Assert.Null(KeyTransferCipher.Open(receiver, ticket, Guid.NewGuid(), sealedKey));
        Assert.Null(KeyTransferCipher.Open(receiver, otherTransfer, s_account, sealedKey));
    }

    [Theory]
    [InlineData(18)]
    [InlineData(40)]
    [InlineData(60)]
    [InlineData(90)]
    public void AChangedByteAnywhereIsNoticed(int at)
    {
        var (receiver, ticket) = NewPhone("t1");
        byte[] sealedKey = KeyTransferCipher.Seal(ContributionKey.Generate(1), ticket, s_account);
        sealedKey[at] ^= 0x01;

        Assert.Null(KeyTransferCipher.Open(receiver, ticket, s_account, sealedKey));
    }

    [Fact]
    public void ATruncatedOrForeignBlobDoesNotOpen()
    {
        var (receiver, ticket) = NewPhone("t1");
        byte[] sealedKey = KeyTransferCipher.Seal(ContributionKey.Generate(1), ticket, s_account);

        Assert.Null(KeyTransferCipher.Open(receiver, ticket, s_account, sealedKey.Take(sealedKey.Length - 1).ToArray()));
        Assert.Null(KeyTransferCipher.Open(receiver, ticket, s_account, sealedKey.Take(30).ToArray()));
        Assert.Null(KeyTransferCipher.Open(receiver, ticket, s_account, []));
    }

    [Fact]
    public void ALowOrderOrMalformedPublicKeyIsRefused()
    {
        var key = ContributionKey.Generate(1);

        Assert.Throws<ArgumentException>(() => KeyTransferCipher.Seal(key, new KeyTransferTicket("t", new byte[32], KeyTransferTicket.NewSecret()), s_account));
        Assert.Throws<ArgumentException>(() => new KeyTransferTicket("t", new byte[31], KeyTransferTicket.NewSecret()));
        Assert.Throws<ArgumentException>(() => new KeyTransferTicket("t", TransferKeys.Generate().PublicKey, new byte[15]));
    }

    [Fact]
    public void TheSameAgreementOnBothSides()
    {
        var a = TransferKeys.Generate();
        var b = TransferKeys.Generate();

        Assert.Equal(a.SharedSecretWith(b.PublicKey), b.SharedSecretWith(a.PublicKey));
        Assert.Equal(32, a.PublicKey.Length);
        Assert.NotEqual(a.PublicKey, b.PublicKey);
    }

    [Fact]
    public void TheAssociatedDataNamesTheAccountAndTheTransfer() =>
        Assert.Equal("eirora-key-transfer 019a0a4e-6b5c-7d2e-9f10-2a3b4c5d6e7f t1", Encoding.UTF8.GetString(KeyTransferCipher.AssociatedData(s_account, "t1")));

    [Fact]
    public void TheTicketIsOneLineWithTheSecretAndReadsBack()
    {
        var (_, ticket) = NewPhone("abc123");

        var read = KeyTransferTicket.TryParse(ticket.ToText());

        Assert.StartsWith("eirora-key-transfer 1 abc123 ", ticket.ToText(), StringComparison.Ordinal);
        Assert.Equal(5, ticket.ToText().Split(' ').Length);
        Assert.Equal("abc123", read!.Id);
        Assert.Equal(ticket.ReceiverPublicKey, read.ReceiverPublicKey);
        Assert.Equal(ticket.Secret, read.Secret);
        Assert.Equal(16, ticket.Secret.Length);
        Assert.NotEqual(KeyTransferTicket.NewSecret(), KeyTransferTicket.NewSecret());
        Assert.Null(KeyTransferTicket.TryParse(ticket.ToText().Replace("eirora-key-transfer 1", "eirora-key-transfer 2")));
        Assert.Null(KeyTransferTicket.TryParse("eirora-key-transfer 1 abc123 " + Convert.ToBase64String(ticket.ReceiverPublicKey)));
        Assert.Null(KeyTransferTicket.TryParse("eirora-key-transfer 1 abc123 AAAA " + Convert.ToBase64String(ticket.Secret)));
        Assert.Null(KeyTransferTicket.TryParse("hello"));
        Assert.Null(KeyTransferTicket.TryParse(null));
    }

    // The text comes from a camera, so a crafted code must give null, never an exception that leaves the scanner stuck.
    [Theory]
    [InlineData("eirora-key-transfer 1  PUB SECRET", "an empty id")]
    [InlineData("eirora-key-transfer 1 a/b PUB SECRET", "a slash in the id")]
    [InlineData("eirora-key-transfer 1 a.b PUB SECRET", "a dot in the id")]
    [InlineData("eirora-key-transfer 1 LONG PUB SECRET", "an id longer than the longest")]
    [InlineData("eirora-key-transfer 1 abc123 PUB SECRET extra", "an extra field")]
    [InlineData("eirora-key-transfer 1 abc123 PUB SHORT", "a secret of the wrong length")]
    [InlineData("eirora-key-transfer 1 abc123 PUB ****", "a secret that is not base64")]
    [InlineData("eirora-key-transfer 1 abc123 LONGPUB SECRET", "a public key of the wrong length")]
    [InlineData("eirora-key-transfer\t1 abc123 PUB SECRET", "a tab for a space")]
    public void ACraftedTicketIsNotOneAndNeverThrows(string pattern, string why)
    {
        var (_, ticket) = NewPhone("abc123");
        string text = pattern
            .Replace("LONGPUB", Convert.ToBase64String(new byte[33]))
            .Replace("PUB", Convert.ToBase64String(ticket.ReceiverPublicKey))
            .Replace("SECRET", Convert.ToBase64String(ticket.Secret))
            .Replace("SHORT", Convert.ToBase64String(new byte[15]))
            .Replace("LONG", new string('a', KeyTransferTicket.MaxIdLength + 1));

        Assert.True(KeyTransferTicket.TryParse(text) is null, why);
    }

    [Fact]
    public void TheCheckCodeIsSixDigitsFromTheWholeTicketAndTheSameOnBothPhones()
    {
        var (_, ticket) = NewPhone("abc123");
        var read = KeyTransferTicket.TryParse(ticket.ToText())!;
        var otherSecret = new KeyTransferTicket("abc123", ticket.ReceiverPublicKey, KeyTransferTicket.NewSecret());
        var otherId = new KeyTransferTicket("abc124", ticket.ReceiverPublicKey, ticket.Secret);

        Assert.Matches("^[0-9]{3} [0-9]{3}$", ticket.CheckCode);
        Assert.Equal(ticket.CheckCode, read.CheckCode);
        Assert.NotEqual(ticket.CheckCode, otherSecret.CheckCode);
        Assert.NotEqual(ticket.CheckCode, otherId.CheckCode);
        Assert.Throws<ArgumentException>(() => new KeyTransferTicket("a b", ticket.ReceiverPublicKey, ticket.Secret));
        Assert.Throws<ArgumentException>(() => new KeyTransferTicket(string.Empty, ticket.ReceiverPublicKey, ticket.Secret));
    }

    private static (TransferKeys Keys, KeyTransferTicket Ticket) NewPhone(string transferId)
    {
        var keys = TransferKeys.Generate();
        return (keys, new KeyTransferTicket(transferId, keys.PublicKey, KeyTransferTicket.NewSecret()));
    }
}
