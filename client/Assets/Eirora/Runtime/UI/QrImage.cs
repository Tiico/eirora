using Eirora.Client.App;
using UnityEngine;

namespace Eirora.Client.UI
{
    /// <summary>A QR code from <see cref="QrCodes.Encode"/> as a texture: one pixel per module, black on white, drawn sharp.</summary>
    internal static class QrImage
    {
        public static Texture2D Of(bool[,] modules)
        {
            int width = modules.GetLength(0);
            int height = modules.GetLength(1);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[width * height];
            var dark = new Color32(0, 0, 0, 255);
            var light = new Color32(255, 255, 255, 255);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixels[((height - 1 - y) * width) + x] = modules[x, y] ? dark : light; // Rows run from the bottom in a texture.
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return texture;
        }
    }
}
