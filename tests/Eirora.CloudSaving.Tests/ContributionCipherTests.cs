using System.Security.Cryptography;
using System.Text;
using FsCheck.Xunit;

namespace Eirora.CloudSaving.Tests;

/// <summary>
/// The contribution part is sealed on the device and opens only with the same key, for the same account, key epoch
/// and version it was sealed for (ADR-0008, D-57, threat model: the server cannot read it or move it).
/// </summary>
public sealed class ContributionCipherTests
{
    private static readonly Guid s_account = Guid.Parse("019a0a4e-6b5c-7d2e-9f10-2a3b4c5d6e7f");
    private static readonly ContributionKey s_key = ContributionKey.Generate(1);

    [Property(MaxTest = 30)]
    public bool WhatWasSealedOpensAgain(byte[] contributions, long version)
    {
        byte[] sealedPart = ContributionCipher.Seal(s_key, s_account, version, contributions);

        return ContributionCipher.Open(s_key, s_account, version, sealedPart) is { } opened && opened.AsSpan().SequenceEqual(contributions);
    }

    [Fact]
    public void TheSealedPartIsAHeaderANonceAndTheCiphertextWithItsTag()
    {
        byte[] contributions = Encoding.UTF8.GetBytes("eirora-save 3\n...");

        byte[] sealedPart = ContributionCipher.Seal(s_key, s_account, 1, contributions);

        Assert.Equal("eirora-sealed 1\n", Encoding.UTF8.GetString(sealedPart, 0, 16));
        Assert.Equal(16 + ContributionCipher.NonceLength + contributions.Length + ContributionCipher.TagLength, sealedPart.Length);
        Assert.False(sealedPart.Skip(16 + ContributionCipher.NonceLength).Take(contributions.Length).SequenceEqual(contributions));
    }

    [Fact]
    public void EverySealGetsItsOwnNonceAndCiphertext()
    {
        byte[] contributions = Encoding.UTF8.GetBytes("the same part");

        byte[] first = ContributionCipher.Seal(s_key, s_account, 1, contributions);
        byte[] second = ContributionCipher.Seal(s_key, s_account, 1, contributions);

        Assert.NotEqual(first.Skip(16).Take(ContributionCipher.NonceLength), second.Skip(16).Take(ContributionCipher.NonceLength));
        Assert.NotEqual(first.Skip(16 + ContributionCipher.NonceLength), second.Skip(16 + ContributionCipher.NonceLength));
    }

    [Fact]
    public void APartSealedForAnotherAccountEpochOrVersionDoesNotOpen()
    {
        byte[] sealedPart = ContributionCipher.Seal(s_key, s_account, 7, Encoding.UTF8.GetBytes("part"));
        var otherEpoch = new ContributionKey(2, KeyBytes(s_key));

        Assert.NotNull(ContributionCipher.Open(s_key, s_account, 7, sealedPart));
        Assert.Null(ContributionCipher.Open(s_key, Guid.NewGuid(), 7, sealedPart));
        Assert.Null(ContributionCipher.Open(otherEpoch, s_account, 7, sealedPart));
        Assert.Null(ContributionCipher.Open(s_key, s_account, 8, sealedPart));
        Assert.Null(ContributionCipher.Open(ContributionKey.Generate(1), s_account, 7, sealedPart));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(30)]
    [InlineData(45)]
    public void AChangedByteAnywhereIsNoticed(int at)
    {
        byte[] sealedPart = ContributionCipher.Seal(s_key, s_account, 1, Encoding.UTF8.GetBytes("a part of some length"));
        sealedPart[at] ^= 0x01;

        Assert.Null(ContributionCipher.Open(s_key, s_account, 1, sealedPart));
    }

    [Fact]
    public void ATruncatedOrForeignPartDoesNotOpen()
    {
        byte[] sealedPart = ContributionCipher.Seal(s_key, s_account, 1, Encoding.UTF8.GetBytes("part"));

        Assert.Null(ContributionCipher.Open(s_key, s_account, 1, sealedPart.Take(sealedPart.Length - 1).ToArray()));
        Assert.Null(ContributionCipher.Open(s_key, s_account, 1, sealedPart.Take(20).ToArray()));
        Assert.Null(ContributionCipher.Open(s_key, s_account, 1, []));
        Assert.Null(ContributionCipher.Open(s_key, s_account, 1, RandomNumberGenerator.GetBytes(200)));
    }

    [Fact]
    public void TheAssociatedDataNamesTheAccountTheEpochAndTheVersion()
    {
        Assert.Equal(
            "eirora-contributions 019a0a4e-6b5c-7d2e-9f10-2a3b4c5d6e7f 3 42",
            Encoding.UTF8.GetString(ContributionCipher.AssociatedData(s_account, 3, 42)));
    }

    [Fact]
    public void AKeyIsKeptAsOneLineAndReadBack()
    {
        var key = ContributionKey.Generate(4);

        var read = ContributionKey.TryParse(key.ToText());

        Assert.Equal(4, read!.Epoch);
        Assert.Equal(KeyBytes(key), KeyBytes(read));
        Assert.Matches("^4 [A-Za-z0-9+/]{43}=$", key.ToText());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("0 AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("1 not-base64")]
    [InlineData("1 AAAA")]
    public void WhatIsNotAKeyIsNotRead(string? text) => Assert.Null(ContributionKey.TryParse(text));

    [Fact]
    public void TwoGeneratedKeysDiffer() => Assert.NotEqual(KeyBytes(ContributionKey.Generate(1)), KeyBytes(ContributionKey.Generate(1)));

    [Fact]
    public void TheStateIsKeptAsTextAndReadBack()
    {
        var state = new CloudSyncState(s_account, 12, 2);

        var read = CloudSyncState.TryParse(state.ToText());

        Assert.Equal("eirora-cloud 1\n019a0a4e-6b5c-7d2e-9f10-2a3b4c5d6e7f 12 2\n", state.ToText());
        Assert.Equal((s_account, 12L, 2), (read!.AccountId, read.Version, read.KeyEpoch));
        Assert.Equal(0, CloudSyncState.TryParse("eirora-cloud 1\n019a0a4e-6b5c-7d2e-9f10-2a3b4c5d6e7f 12 0\n")!.KeyEpoch); // Without a contribution part (TASK-060).
        Assert.Null(CloudSyncState.TryParse("eirora-cloud 1\n019a0a4e-6b5c-7d2e-9f10-2a3b4c5d6e7f 12 -1\n"));
        Assert.Equal(7, CloudSyncState.TryParse(new CloudSyncState(s_account, 12, 0, 7).ToText())!.AcceptedCommands);
        Assert.Null(CloudSyncState.TryParse("eirora-cloud 1\n019a0a4e-6b5c-7d2e-9f10-2a3b4c5d6e7f 12 2\n")!.AcceptedCommands); // Written before the count was kept.
        Assert.Null(CloudSyncState.TryParse("something else"));
        Assert.Null(CloudSyncState.TryParse(null));
    }

    private static byte[] KeyBytes(ContributionKey key) => key.ToBytes();
}
