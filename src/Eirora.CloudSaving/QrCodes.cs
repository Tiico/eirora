using System.Collections.Generic;
using System.Linq;
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

        /// <summary>Pixels a module at which <see cref="Encode"/> checks that its code is read.</summary>
        private static readonly int[] s_checkedScales = { 2, 3, 4, 5 };

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
        /// <remarks>
        /// ZXing's reader looks for the three finder patterns from the top of the picture down and stops at the first
        /// three that agree in size. A spot in the data that passes its finder test before the last finder is reached
        /// takes that finder's place, and the code is not read (#160). The QR standard's mask penalty only counts the
        /// exact 1:1:3:1:1 run with four light modules beside it, so the mask ZXing chooses leaves such a spot in about
        /// one ticket in two hundred. So the masks are tried from the lowest penalty up, and the first whose code the
        /// reader reads at every quarter turn and at a few sizes is the one shown.
        /// </remarks>
        public static bool[,] Encode(string text)
        {
            var hints = new Dictionary<EncodeHintType, object> { [EncodeHintType.ERROR_CORRECTION] = ErrorCorrectionLevel.M };
            var codes = new List<(bool[,] Modules, int Penalty)>();
            for (int mask = 0; mask < QRCode.NUM_MASK_PATTERNS; mask++)
            {
                hints[EncodeHintType.QR_MASK_PATTERN] = mask;
                var matrix = Encoder.encode(text, ErrorCorrectionLevel.M, hints).Matrix;
                int penalty = MaskUtil.applyMaskPenaltyRule1(matrix) + MaskUtil.applyMaskPenaltyRule2(matrix)
                    + MaskUtil.applyMaskPenaltyRule3(matrix) + MaskUtil.applyMaskPenaltyRule4(matrix);
                codes.Add((WithQuietZone(matrix), penalty));
            }

            // A stable sort, so that of equal penalties the lower mask wins, as in ZXing's own choice.
            var byPenalty = codes.OrderBy(code => code.Penalty).ToList();
            foreach (var (modules, _) in byPenalty)
            {
                if (IsReadAsShown(modules, text))
                {
                    return modules;
                }
            }

            return byPenalty[0].Modules;
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

        /// <summary>The code's modules, true where dark, with the quiet zone around them.</summary>
        private static bool[,] WithQuietZone(ByteMatrix matrix)
        {
            var modules = new bool[matrix.Width + (2 * QuietZone), matrix.Height + (2 * QuietZone)];
            for (int y = 0; y < matrix.Height; y++)
            {
                for (int x = 0; x < matrix.Width; x++)
                {
                    modules[x + QuietZone, y + QuietZone] = matrix[x, y] == 1;
                }
            }

            return modules;
        }

        /// <summary>
        /// Whether <see cref="Decode"/> reads <paramref name="text"/> from the code drawn sharp at
        /// <see cref="s_checkedScales"/> pixels a module and turned by each quarter turn: the turns start the reader's
        /// search from each side of the code.
        /// </summary>
        private static bool IsReadAsShown(bool[,] modules, string text)
        {
            int side = modules.GetLength(0);
            foreach (int scale in s_checkedScales)
            {
                int size = side * scale;
                var grey = new byte[size * size];
                for (int quarterTurns = 0; quarterTurns < 4; quarterTurns++)
                {
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

                            grey[(y * size) + x] = modules[column, row] ? (byte)0 : (byte)255;
                        }
                    }

                    if (Decode(grey, size, size) != text)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
