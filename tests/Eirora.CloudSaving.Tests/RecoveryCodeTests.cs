using FsCheck.Xunit;

namespace Eirora.CloudSaving.Tests;

/// <summary>The recovery code (ADR-0008, TASK-023b): the key as 64 characters a player can type, with a check against a wrong one.</summary>
public sealed class RecoveryCodeTests
{
    [Property(MaxTest = 30)]
    public bool TheCodeReadsBackToTheKey(int epochSeed)
    {
        int epoch = 1 + Math.Abs(epochSeed % 60000);
        var key = ContributionKey.Generate(epoch);

        var read = RecoveryCode.TryParse(RecoveryCode.Of(key));

        return read is not null && read.Epoch == epoch && read.ToText() == key.ToText();
    }

    [Fact]
    public void TheCodeIsEightGroupsOfEightFromTheCrockfordAlphabet()
    {
        string code = RecoveryCode.Of(ContributionKey.Generate(3));

        Assert.Matches("^([0-9A-HJKMNP-TV-Z]{8}-){7}[0-9A-HJKMNP-TV-Z]{8}$", code);
        Assert.Equal(71, code.Length);
    }

    [Fact]
    public void CaseHyphensSpacesAndLookAlikeLettersDoNotMatter()
    {
        var key = ContributionKey.Generate(2);
        string code = RecoveryCode.Of(key);
        string typed = code.ToLowerInvariant().Replace("-", " ").Replace('0', 'o').Replace('1', 'i');

        Assert.Equal(key.ToText(), RecoveryCode.TryParse(typed)!.ToText());
        Assert.Equal(key.ToText(), RecoveryCode.TryParse(code.Replace("-", string.Empty).Replace('1', 'L'))!.ToText());
    }

    [Property(MaxTest = 40)]
    public bool AWrongCharacterIsCaught(int position, int shift)
    {
        string code = RecoveryCode.Of(ContributionKey.Generate(1)).Replace("-", string.Empty);
        int at = Math.Abs(position % code.Length);
        const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        char wrong = Alphabet[(Alphabet.IndexOf(code[at]) + 1 + Math.Abs(shift % 31)) % 32];
        string typo = code.Substring(0, at) + wrong + code.Substring(at + 1);

        return RecoveryCode.TryParse(typo) is null;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ABCD-EFGH")]
    [InlineData("UUUUUUUU-UUUUUUUU-UUUUUUUU-UUUUUUUU-UUUUUUUU-UUUUUUUU-UUUUUUUU-UUUUUUUU")]
    public void WhatIsNotACodeIsNotRead(string? text) => Assert.Null(RecoveryCode.TryParse(text));

    [Fact]
    public void AnEpochThatDoesNotFitIsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RecoveryCode.Of(new ContributionKey(70000, new byte[32])));

    [Fact]
    public void TwoKeysHaveDifferentCodes() =>
        Assert.NotEqual(RecoveryCode.Of(ContributionKey.Generate(1)), RecoveryCode.Of(ContributionKey.Generate(1)));
}
