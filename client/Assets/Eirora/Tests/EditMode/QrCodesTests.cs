using System;
using Eirora.Client.App;
using Eirora.CloudSaving;
using NUnit.Framework;

namespace Eirora.Client.Tests
{
    /// <summary>The QR code carries the key transfer ticket from the new phone's screen to the old phone's camera (TASK-023b).</summary>
    public sealed class QrCodesTests
    {
        [Test]
        public void ATicketShownAsACodeIsReadBackWhole()
        {
            var ticket = new KeyTransferTicket("3f2b9c1e4d5a6b7c8d9e0f1a2b3c4d5e", TransferKeys.Generate().PublicKey, KeyTransferTicket.NewSecret()).ToText();

            var modules = QrCodes.Encode(ticket);
            var (frame, width, height) = Picture(modules, scale: 4, quarterTurns: 0);

            Assert.That(modules.GetLength(0), Is.EqualTo(modules.GetLength(1)), "a QR code is square");
            Assert.That(modules[0, 0], Is.False, "the quiet zone is light");
            Assert.That(modules[QrCodes.QuietZone, QrCodes.QuietZone], Is.True, "a finder pattern begins the code");
            Assert.That(QrCodes.Decode(frame, width, height), Is.EqualTo(ticket));
            Assert.That(KeyTransferTicket.TryParse(QrCodes.Decode(frame, width, height)), Is.Not.Null);
        }

        [Test]
        public void TheCodeIsReadAtAnyAngle()
        {
            string text = "eirora-key-transfer 1 id " + Convert.ToBase64String(new byte[32]) + " " + Convert.ToBase64String(new byte[16]);
            var modules = QrCodes.Encode(text);

            foreach (int quarterTurns in new[] { 1, 2, 3 })
            {
                var (frame, width, height) = Picture(modules, scale: 3, quarterTurns);
                Assert.That(QrCodes.Decode(frame, width, height), Is.EqualTo(text), $"turned {quarterTurns * 90} degrees");
            }
        }

        [Test]
        public void APictureWithoutACodeGivesNothing()
        {
            var frame = new byte[64 * 48];
            for (int i = 0; i < frame.Length; i++)
            {
                frame[i] = (byte)(i % 7 == 0 ? 20 : 200);
            }

            Assert.That(QrCodes.Decode(frame, 64, 48), Is.Null);
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
}
