using Eirora.Client.App;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace Eirora.Client.UI
{
    /// <summary>
    /// The phone's rear camera through Unity's <see cref="WebCamTexture"/>, read for a QR code with ZXing.Net
    /// (<see cref="QrCodes"/>). On Android the camera permission is asked for here, at first use; on iOS Unity asks with
    /// the text in the player settings (ProjectSetup). The frames stay on the phone.
    /// </summary>
    public sealed class WebCamQrScanner : IQrScanner
    {
        private const int Width = 640;
        private const int Height = 480;
        private const int Fps = 30;

        private WebCamTexture? _camera;
        private Color32[]? _pixels;
        private byte[]? _grey;

        public ScannerStatus Status { get; private set; } = ScannerStatus.Starting;

        public Texture? Preview => _camera;

        public int PreviewRotation => _camera is { } camera ? camera.videoRotationAngle : 0;

        public bool PreviewMirrored => _camera is { } camera && camera.videoVerticallyMirrored;

        public void Start()
        {
            if (_camera is not null)
            {
                return;
            }

            Status = ScannerStatus.Starting;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => Open();
                callbacks.PermissionDenied += _ => Status = ScannerStatus.Denied;
                Permission.RequestUserPermission(Permission.Camera, callbacks);
                return;
            }

            Open();
#else
            if (Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                Open();
                return;
            }

            var request = Application.RequestUserAuthorization(UserAuthorization.WebCam);
            request.completed += _ =>
            {
                if (Application.HasUserAuthorization(UserAuthorization.WebCam))
                {
                    Open();
                }
                else
                {
                    Status = ScannerStatus.Denied;
                }
            };
#endif
        }

        public string? Read()
        {
            if (_camera is not { } camera || !camera.isPlaying || !camera.didUpdateThisFrame || camera.width < 32 || camera.height < 32)
            {
                return null;
            }

            int width = camera.width;
            int height = camera.height;
            if (_pixels is null || _pixels.Length != width * height)
            {
                _pixels = new Color32[width * height];
                _grey = new byte[width * height];
            }

            camera.GetPixels32(_pixels);
            // The texture's rows run from the bottom; the reader wants them from the top, or the code is a mirror image.
            for (int y = 0; y < height; y++)
            {
                int from = (height - 1 - y) * width;
                int to = y * width;
                for (int x = 0; x < width; x++)
                {
                    var pixel = _pixels[from + x];
                    _grey![to + x] = (byte)(((77 * pixel.r) + (151 * pixel.g) + (28 * pixel.b)) >> 8);
                }
            }

            return QrCodes.Decode(_grey!, width, height);
        }

        public void Stop()
        {
            if (_camera is not { } camera)
            {
                return;
            }

            camera.Stop();
            Object.Destroy(camera);
            _camera = null;
            Status = ScannerStatus.Starting;
        }

        private void Open()
        {
            if (_camera is not null)
            {
                return;
            }

            var devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                Status = ScannerStatus.NoCamera;
                return;
            }

            string name = devices[0].name;
            foreach (var device in devices)
            {
                if (!device.isFrontFacing)
                {
                    name = device.name; // The rear camera is the one pointed at the other phone.
                    break;
                }
            }

            _camera = new WebCamTexture(name, Width, Height, Fps);
            _camera.Play();
            Status = ScannerStatus.Scanning;
        }
    }
}
