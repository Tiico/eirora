using Eirora.Simulation.Saving;

namespace Eirora.CloudSaving
{
    /// <summary>The world on the device, as the reconciliation with the cloud save sees it: the game session in the client.</summary>
    public interface ILocalWorld
    {
        /// <summary>The device's save, or null before a world exists.</summary>
        Save? Current { get; }

        /// <summary>
        /// Makes <paramref name="save"/> the device's world and writes it. A world it replaces is kept aside on the
        /// device, never deleted, so that a wrong choice can be undone by hand.
        /// </summary>
        void Replace(Save save, ReplaceReason reason);

        /// <summary>Keeps a world that was not chosen aside on the device: the cloud's, when the player kept the phone's.</summary>
        void KeepAside(Save save);
    }

    public enum ReplaceReason
    {
        /// <summary>The device's journal and the cloud save's were merged (D-99).</summary>
        Merged,

        /// <summary>The device had no world; the cloud save's came down.</summary>
        Downloaded,

        /// <summary>The player chose the cloud's world over the phone's.</summary>
        CloudChosen,
    }
}
