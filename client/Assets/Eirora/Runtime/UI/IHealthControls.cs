using Eirora.Client.Health;

namespace Eirora.Client.UI
{
    /// <summary>What the self-care sheet can do with the health store (HLT-03, HLT-12).</summary>
    public interface IHealthControls
    {
        /// <summary>The store's state when the game was last opened, or null before it is known or without a store.</summary>
        HealthStatus? Status { get; }

        /// <summary>Asks for steps, workouts and sleep; reports how many were granted.</summary>
        void Connect(System.Action<int> done);

        void OpenSettings();
    }
}
