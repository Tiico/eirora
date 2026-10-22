using System.Text;
using FsCheck.Xunit;

namespace Eirora.CloudSaving.Tests;

/// <summary>
/// The contribution key sealed for one other phone (ADR-0008, TASK-023b): only the phone with the private half of the
/// ticket's key opens it, and only in the transfer it was sealed for; the server, which relays it, cannot.
/// </summary>
public sealed class KeyTransferCipherTests
{
    private static readonly Guid s_account = Guid.Parse("019a0a4e-6b5c-7d2e-9f10-2a3b4c5d6e7f");

    [Property(MaxTest = 20)]
    public bool TheKeyOpensOnTheReceivingPhone(int epochSeed)
    {
        int epoch = 1 + Math.Abs(epochSeed % 1000);
        var key = ContributionKey.Generate(epoch);
        var receiver = TransferKeys.Generate();

        byte[] sealedKey = KeyTransferCipher.Seal(key, receiver.PublicKey, s_account, "t1");
        var opened = KeyTransferCipher.Open(receiver, s_account, "t1", sealedKey);

        return opened is not null && opened.Epoch == epoch && opened.ToText() == key.ToText();
    }

    [Fact]
    public void TheSealedKeyIsAHeaderTheSendersKeyANonceAndTheCiphertext()
    {
        var key = ContributionKey.Generate(1);

        byte[] sealedKey = KeyTransferCipher.Seal(key, TransferKeys.Generate().PublicKey, s_account, "t1");

        Assert.Equal("eirora-transfer 1\n", Encoding.UTF8.GetString(sealedKey, 0, 18));
        Assert.Equal(18 + 32 + 24 + key.ToText().Length + 16, sealedKey.Length);
        Assert.DoesNotContain(key.ToText().Substring(2, 20), Encoding.Latin1.GetString(sealedKey), StringComparison.Ordinal);
    }

    [Fact]
    public void EverySealUsesItsOwnSenderKeyAndNonce()
    {
        var key = ContributionKey.Generate(1);
        var receiver = TransferKeys.Generate();

        byte[] first = KeyTransferCipher.Seal(key, receiver.PublicKey, s_account, "t1");
        byte[] second = KeyTransferCipher.Seal(key, receiver.PublicKey, s_account, "t1");

        Assert.NotEqual(first.Skip(18).Take(32), second.Skip(18).Take(32));
        Assert.NotEqual(first.Skip(50).Take(24), second.Skip(50).Take(24));
    }

    [Fact]
    public void AnotherPhoneAccountOrTransferDoesNotOpenIt()
    {
        var key = ContributionKey.Generate(1);
        var receiver = TransferKeys.Generate();
        byte[] sealedKey = KeyTransferCipher.Seal(key, receiver.PublicKey, s_account, "t1");

        Assert.NotNull(KeyTransferCipher.Open(receiver, s_account, "t1", sealedKey));
        Assert.Null(KeyTransferCipher.Open(TransferKeys.Generate(), s_account, "t1", sealedKey));
        Assert.Null(KeyTransferCipher.Open(receiver, Guid.NewGuid(), "t1", sealedKey));
        Assert.Null(KeyTransferCipher.Open(receiver, s_account, "t2", sealedKey));
    }

    [Theory]
    [InlineData(18)]
    [InlineData(40)]
    [InlineData(60)]
    [InlineData(90)]
    public void AChangedByteAnywhereIsNoticed(int at)
    {
        var receiver = TransferKeys.Generate();
        byte[] sealedKey = KeyTransferCipher.Seal(ContributionKey.Generate(1), receiver.PublicKey, s_account, "t1");
        sealedKey[at] ^= 0x01;

        Assert.Null(KeyTransferCipher.Open(receiver, s_account, "t1", sealedKey));
    }

    [Fact]
    public void ATruncatedOrForeignBlobDoesNotOpen()
    {
        var receiver = TransferKeys.Generate();
        byte[] sealedKey = KeyTransferCipher.Seal(ContributionKey.Generate(1), receiver.PublicKey, s_account, "t1");

        Assert.Null(KeyTransferCipher.Open(receiver, s_account, "t1", sealedKey.Take(sealedKey.Length - 1).ToArray()));
        Assert.Null(KeyTransferCipher.Open(receiver, s_account, "t1", sealedKey.Take(30).ToArray()));
        Assert.Null(KeyTransferCipher.Open(receiver, s_account, "t1", []));
    }

    [Fact]
    public void ALowOrderOrMalformedPublicKeyIsRefused()
    {
        var key = ContributionKey.Generate(1);

        Assert.Throws<ArgumentException>(() => KeyTransferCipher.Seal(key, new byte[32], s_account, "t1"));
        Assert.Throws<ArgumentException>(() => KeyTransferCipher.Seal(key, new byte[31], s_account, "t1"));
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
    public void TheTicketIsOneLineAndReadsBack()
    {
        var receiver = TransferKeys.Generate();
        var ticket = new KeyTransferTicket("abc123", receiver.PublicKey);

        var read = KeyTransferTicket.TryParse(ticket.ToText());

        Assert.StartsWith("eirora-key-transfer 1 abc123 ", ticket.ToText(), StringComparison.Ordinal);
        Assert.Equal("abc123", read!.Id);
        Assert.Equal(receiver.PublicKey, read.ReceiverPublicKey);
        Assert.Null(KeyTransferTicket.TryParse("eirora-key-transfer 2 abc123 " + Convert.ToBase64String(receiver.PublicKey)));
        Assert.Null(KeyTransferTicket.TryParse("eirora-key-transfer 1 abc123 AAAA"));
        Assert.Null(KeyTransferTicket.TryParse("hello"));
        Assert.Null(KeyTransferTicket.TryParse(null));
    }
}
