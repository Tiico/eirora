using Eirora.Client.UI;
using Eirora.CloudSaving;
using NUnit.Framework;
using UnityEngine;

namespace Eirora.Client.Tests
{
    /// <summary>The old phone reads the new phone's ticket from camera frames, also those it reads at every other pixel (TASK-023b, #160).</summary>
    public sealed class WebCamQrScannerTests
    {
        // Tickets whose codes ZXing's own choice of mask left unreadable (#160).
        private const string FirstTicket = "eirora-key-transfer 1 3f2b9c1e4d5a6b7c8d9e0f1a2b3c4d5e 8nAAtLUH51a6MMSGx1EtAMPEdB1ei3z6MVGpmLrcy6Q= y+I32ZuYOm5yayy9aV/z1A==";
        private const string SecondTicket = "eirora-key-transfer 1 3f2b9c1e4d5a6b7c8d9e0f1a2b3c4d5e 72Cphh4tnWtQAmBmZvgOXQqjCpE3G5+Hr19/8DYeDyg= Xk4Pr5SwEMawL8YixIk2mw==";

        [TestCase(FirstTicket, 640, 480, 5)]
        [TestCase(FirstTicket, 1280, 720, 8)]
        [TestCase(SecondTicket, 640, 480, 4)]
        [TestCase(SecondTicket, 1920, 1080, 11)]
        public void ATicketInACameraFrameIsReadBackWhole(string ticket, int width, int height, int scale)
        {
            var pixels = Frame(QrCodes.Encode(ticket), width, height, scale);
            byte[]? grey = null;

            Assert.That(WebCamQrScanner.ReadFrame(pixels, width, height, ref grey), Is.EqualTo(ticket));
            Assert.That(KeyTransferTicket.TryParse(ticket), Is.Not.Null);
        }

        [Test]
        public void AFrameWithoutACodeGivesNothing()
        {
            var pixels = new Color32[320 * 240];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = i % 7 == 0 ? new Color32(20, 20, 20, 255) : new Color32(200, 200, 200, 255);
            }

            byte[]? grey = null;

            Assert.That(WebCamQrScanner.ReadFrame(pixels, 320, 240, ref grey), Is.Null);
        }

        /// <summary>The code in the middle of a grey camera frame, each module <paramref name="scale"/> pixels wide, rows from the bottom as in a texture.</summary>
        private static Color32[] Frame(bool[,] modules, int width, int height, int scale)
        {
            int side = modules.GetLength(0);
            int left = (width - (side * scale)) / 2;
            int top = (height - (side * scale)) / 2;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int column = (x - left) / scale;
                    int row = (y - top) / scale;
                    bool inside = x >= left && y >= top && column < side && row < side;
                    byte value = !inside ? (byte)120 : modules[column, row] ? (byte)35 : (byte)230;
                    pixels[((height - 1 - y) * width) + x] = new Color32(value, value, value, 255);
                }
            }

            return pixels;
        }
    }
}
