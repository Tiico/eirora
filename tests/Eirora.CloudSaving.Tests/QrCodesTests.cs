using System;

namespace Eirora.CloudSaving.Tests;

/// <summary>The QR code carries the key transfer ticket from the new phone's screen to the old phone's camera (TASK-023b).</summary>
public sealed class QrCodesTests
{
    [Fact]
    public void ATicketShownAsACodeIsReadBackWhole()
    {
        var ticket = new KeyTransferTicket("3f2b9c1e4d5a6b7c8d9e0f1a2b3c4d5e", TransferKeys.Generate().PublicKey, KeyTransferTicket.NewSecret()).ToText();

        var modules = QrCodes.Encode(ticket);
        var (frame, width, height) = Picture(modules, scale: 4, quarterTurns: 0);

        Assert.Equal(modules.GetLength(0), modules.GetLength(1));
        Assert.False(modules[0, 0], "the quiet zone is light");
        Assert.True(modules[QrCodes.QuietZone, QrCodes.QuietZone], "a finder pattern begins the code");
        Assert.Equal(ticket, QrCodes.Decode(frame, width, height));
        Assert.NotNull(KeyTransferTicket.TryParse(QrCodes.Decode(frame, width, height)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TheCodeIsReadAtAnyAngle(int quarterTurns)
    {
        string text = "eirora-key-transfer 1 id " + Convert.ToBase64String(new byte[32]) + " " + Convert.ToBase64String(new byte[16]);
        var modules = QrCodes.Encode(text);

        var (frame, width, height) = Picture(modules, scale: 3, quarterTurns);

        Assert.Equal(text, QrCodes.Decode(frame, width, height));
    }

    [Fact]
    public void APictureWithoutACodeGivesNothing()
    {
        var frame = new byte[64 * 48];
        for (int i = 0; i < frame.Length; i++)
        {
            frame[i] = (byte)(i % 7 == 0 ? 20 : 200);
        }

        Assert.Null(QrCodes.Decode(frame, 64, 48));
    }

    /// <summary>The code as a camera might see it: each module <paramref name="scale"/> pixels wide, turned by quarter turns, grey bytes from the top row down.</summary>
    private static (byte[] Grey, int Width, int Height) Picture(bool[,] modules, int scale, int quarterTurns)
    {
        int side = modules.GetLength(0);
        int size = side * scale;
        var grey = new byte[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int column = x / scale;
                int row = y / scale;
                for (int turn = 0; turn < quarterTurns; turn++)
                {
                    (column, row) = (row, side - 1 - column);
                }

                grey[(y * size) + x] = modules[column, row] ? (byte)30 : (byte)225;
            }
        }

        return (grey, size, size);
    }
}
