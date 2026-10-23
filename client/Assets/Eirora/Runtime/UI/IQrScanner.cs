using UnityEngine;

namespace Eirora.Client.UI
{
    /// <summary>How the camera stands while the old phone looks for the new phone's QR code (TASK-023b).</summary>
    public enum ScannerStatus
    {
        /// <summary>The camera is being opened, or the player is being asked to allow it.</summary>
        Starting,

        /// <summary>Frames are coming; <see cref="IQrScanner.Read"/> looks for the code in them.</summary>
        Scanning,

        /// <summary>The player did not allow the camera; the phone's settings can.</summary>
        Denied,

        /// <summary>The phone has no camera that can be used.</summary>
        NoCamera,
    }

    /// <summary>
    /// The camera as a QR code reader, for the key transfer (TASK-023b). The interface starts it when the player wants to
    /// send the key, reads a frame now and then, and stops it as soon as the sheet closes: the camera runs only while
    /// the player looks for the code.
    /// </summary>
    public interface IQrScanner
    {
        ScannerStatus Status { get; }

        /// <summary>The camera's picture to show the player, once <see cref="Status"/> is <see cref="ScannerStatus.Scanning"/>.</summary>
        Texture? Preview { get; }

        /// <summary>Degrees the preview must be turned clockwise to stand upright.</summary>
        int PreviewRotation { get; }

        /// <summary>Whether the preview is upside down and must be flipped.</summary>
        bool PreviewMirrored { get; }

        /// <summary>Opens the camera, asking the player to allow it when needed; nothing happens when it is open already.</summary>
        void Start();

        /// <summary>The text of a QR code in the latest frame, or null when there is none yet.</summary>
        string? Read();

        /// <summary>Closes the camera.</summary>
        void Stop();
    }
}
