using System.Collections.Generic;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.QrCode.Internal;

namespace Eirora.CloudSaving
{
    /// <summary>
    /// QR codes for the key transfer (ADR-0008, TASK-023b): the new phone shows its ticket as one, the old phone reads
    /// it from the camera. Pure bits and bytes, so that .NET tests it headless; the pixels come from, and go to, Unity.
    /// </summary>
    public static class QrCodes
    {
        /// <summary>Modules of white around the code, which readers need to find it (the QR standard's quiet zone).</summary>
        public const int QuietZone = 4;

        private static readonly object s_gate = new();

        // One reader for all frames: it keeps its buffers, and a frame is read every 250 ms while the camera is open.
        // QR codes are found by their finder patterns at any angle, so no rotated retries are needed.
        private static readonly BarcodeReaderGeneric s_reader = new()
        {
            AutoRotate = false,
            Options =
            {
                PossibleFormats = new[] { BarcodeFormat.QR_CODE },
                TryHarder = true,
            },
        };

        /// <summary>
        /// The code for <paramref name="text"/>: true where a module is dark, with the quiet zone around it. The first
        /// index is the column, the second the row.
        /// </summary>
        public static bool[,] Encode(string text)
        {
            var hints = new Dictionary<EncodeHintType, object>
            {
                [EncodeHintType.ERROR_CORRECTION] = ErrorCorrectionLevel.M,
                [EncodeHintType.MARGIN] = 0,
            };
            var matrix = new QRCodeWriter().encode(text, BarcodeFormat.QR_CODE, 0, 0, hints);
            var modules = new bool[matrix.Width + (2 * QuietZone), matrix.Height + (2 * QuietZone)];
            for (int y = 0; y < matrix.Height; y++)
            {
                for (int x = 0; x < matrix.Width; x++)
                {
                    modules[x + QuietZone, y + QuietZone] = matrix[x, y];
                }
            }

            return modules;
        }

        /// <summary>
        /// The text of the QR code in a grey frame of <paramref name="width"/> × <paramref name="height"/> pixels, one
        /// byte each, rows from the top; null when the frame holds none. The code may be at any angle.
        /// </summary>
        public static string? Decode(byte[] grey, int width, int height)
        {
            lock (s_gate)
            {
                return s_reader.Decode(grey, width, height, RGBLuminanceSource.BitmapFormat.Gray8)?.Text;
            }
        }
    }
}
