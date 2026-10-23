using System;
using System.Collections.Generic;
using ZXing;
using ZXing.QrCode;
using ZXing.QrCode.Internal;

namespace Eirora.CloudSaving.Tests;

/// <summary>The QR code carries the key transfer ticket from the new phone's screen to the old phone's camera (TASK-023b).</summary>
public sealed class QrCodesTests
{
    /// <summary>
    /// Tickets whose codes, with the mask ZXing chooses, hold a spot that its reader takes for the bottom left finder
    /// pattern, so that the code was not read (#160). Found among 5000 tickets from <c>new Random(1234)</c>.
    /// </summary>
    public static readonly TheoryData<string> TicketsThatWereNotRead = new()
    {
        "eirora-key-transfer 1 3f2b9c1e4d5a6b7c8d9e0f1a2b3c4d5e 8nAAtLUH51a6MMSGx1EtAMPEdB1ei3z6MVGpmLrcy6Q= y+I32ZuYOm5yayy9aV/z1A==",
        "eirora-key-transfer 1 3f2b9c1e4d5a6b7c8d9e0f1a2b3c4d5e tVFghejg4oQVUwuvM9lgZbk77H5QppF61eOErAzp3yU= CPOpCRRto+lfOANqHUdueg==",
        "eirora-key-transfer 1 3f2b9c1e4d5a6b7c8d9e0f1a2b3c4d5e 72Cphh4tnWtQAmBmZvgOXQqjCpE3G5+Hr19/8DYeDyg= Xk4Pr5SwEMawL8YixIk2mw==",
        "eirora-key-transfer 1 3f2b9c1e4d5a6b7c8d9e0f1a2b3c4d5e JRhiqAUMDe6wjdjxHk8X6kFKNX5djvYOsG/b+oykmPw= TsbmqXSAq0VULPneyp40Pg==",
        "eirora-key-transfer 1 3f2b9c1e4d5a6b7c8d9e0f1a2b3c4d5e TKF9xnInfez3pZ3d3QdZho/eZx4LUT3e/hyk5srlnIc= 0w6ljJvsgS8/FfGFB5e1GA==",
        "eirora-key-transfer 1 3f2b9c1e4d5a6b7c8d9e0f1a2b3c4d5e d+0ZUHdH6oZn632eYH4nwG98lcMyU+CIWUxHVhp/rNk= L/J0X1yUlF05imtyXUhCug==",
        "eirora-key-transfer 1 3f2b9c1e4d5a6b7c8d9e0f1a2b3c4d5e 3+QwY0GlXLF4hHU9TmTSUZ5uIaPjJRCHfLMEkxPr02A= W6x6YwH6QuBKfwrulOqJbg==",
        "eirora-key-transfer 1 3f2b9c1e4d5a6b7c8d9e0f1a2b3c4d5e xp6KFlT7gIZ82WCOiQ7I0sP6YjjSRIEw7rbTR5wfbLs= 6viXsbaXY+8HoguUWCaJIA==",
        "eirora-key-transfer 1 3f2b9c1e4d5a6b7c8d9e0f1a2b3c4d5e ZsgMavIjoNwYBHqJ0zF88cWXLxEciNkVeq2rLMZNfPw= m1ZLsD/D1jxfoe2QXEp9HA==",
        "eirora-key-transfer 1 3f2b9c1e4d5a6b7c8d9e0f1a2b3c4d5e SgxeAbhAHo1Px3koM2RJEwF18hT2S+rV8OdhZY1uv3g= DbQTOOXauwSfmSHqpERz1A==",
    };

    [Fact]
    public void ATicketShownAsACodeIsReadBackWhole()
    {
        var ticket = Ticket(new Random(23));

        var modules = QrCodes.Encode(ticket);
        var (frame, width, height) = Picture(modules, scale: 4, quarterTurns: 0);

        Assert.Equal(modules.GetLength(0), modules.GetLength(1));
        Assert.False(modules[0, 0], "the quiet zone is light");
        Assert.True(modules[QrCodes.QuietZone, QrCodes.QuietZone], "a finder pattern begins the code");
        Assert.Equal(ticket, QrCodes.Decode(frame, width, height));
        Assert.NotNull(KeyTransferTicket.TryParse(QrCodes.Decode(frame, width, height)));
    }

    [Fact]
    public void EveryTicketIsReadBackWhole()
    {
        var random = new Random(160);
        var unread = new List<string>();
        for (int i = 0; i < 1000; i++)
        {
            string ticket = Ticket(random);
            var (frame, width, height) = Picture(QrCodes.Encode(ticket), scale: 4, quarterTurns: i % 4);
            if (QrCodes.Decode(frame, width, height) != ticket)
            {
                unread.Add(ticket);
            }
        }

        Assert.Empty(unread);
    }

    [Theory]
    [MemberData(nameof(TicketsThatWereNotRead))]
    public void ATicketWhoseCodeWasNotReadIsReadNow(string ticket)
    {
        var (frame, width, height) = Picture(WithZXingsMask(ticket), scale: 4, quarterTurns: 0);
        Assert.Null(QrCodes.Decode(frame, width, height));

        Assert.True(IsReadAtEverySizeAndTurn(QrCodes.Encode(ticket), ticket));
    }

    [Fact]
    public void ZXingsMaskIsKeptWhenItsCodeIsRead()
    {
        var random = new Random(7);
        int kept = 0;
        for (int i = 0; i < 50; i++)
        {
            string ticket = Ticket(random);
            var zxings = WithZXingsMask(ticket);
            if (IsReadAtEverySizeAndTurn(zxings, ticket))
            {
                Assert.Equal(zxings, QrCodes.Encode(ticket));
                kept++;
            }
        }

        Assert.InRange(kept, 45, 50);
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

    private static string Ticket(Random random)
    {
        var publicKey = new byte[TransferKeys.PublicKeyLength];
        var secret = new byte[KeyTransferTicket.SecretLength];
        random.NextBytes(publicKey);
        random.NextBytes(secret);
        return new KeyTransferTicket("3f2b9c1e4d5a6b7c8d9e0f1a2b3c4d5e", publicKey, secret).ToText();
    }

    /// <summary>The code as ZXing makes it on its own, with the mask of the lowest penalty, and the quiet zone around it.</summary>
    private static bool[,] WithZXingsMask(string text)
    {
        var hints = new Dictionary<EncodeHintType, object> { [EncodeHintType.ERROR_CORRECTION] = ErrorCorrectionLevel.M, [EncodeHintType.MARGIN] = 0 };
        var matrix = new QRCodeWriter().encode(text, BarcodeFormat.QR_CODE, 0, 0, hints);
        var modules = new bool[matrix.Width + (2 * QrCodes.QuietZone), matrix.Height + (2 * QrCodes.QuietZone)];
        for (int y = 0; y < matrix.Height; y++)
        {
            for (int x = 0; x < matrix.Width; x++)
            {
                modules[x + QrCodes.QuietZone, y + QrCodes.QuietZone] = matrix[x, y];
            }
        }

        return modules;
    }

    /// <summary>Whether the code gives <paramref name="text"/> back at 2 to 8 pixels a module, at every quarter turn.</summary>
    private static bool IsReadAtEverySizeAndTurn(bool[,] modules, string text)
    {
        for (int scale = 2; scale <= 8; scale++)
        {
            for (int quarterTurns = 0; quarterTurns < 4; quarterTurns++)
            {
                var (frame, width, height) = Picture(modules, scale, quarterTurns);
                if (QrCodes.Decode(frame, width, height) != text)
                {
                    return false;
                }
            }
        }

        return true;
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
