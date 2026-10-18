using System;

namespace Eirora.Simulation.Saving
{
    /// <summary>
    /// A newer app wrote the save or snapshot: a newer format, rules or content. It is intact, so it is never
    /// overwritten; the player is told to update the app (ADR-0006).
    /// </summary>
    public sealed class SavedByNewerVersionException : FormatException
    {
        public SavedByNewerVersionException(string message)
            : base(message)
        {
        }
    }
}
