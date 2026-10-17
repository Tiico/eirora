using Eirora.Simulation.Forces;

namespace Eirora.Simulation.Events
{
    /// <summary>A fact about a change in the world (GLOSSARY: Händelse); the client builds reports from these.</summary>
    public abstract class GameEvent
    {
        protected GameEvent(long utcSeconds) => UtcSeconds = utcSeconds;

        /// <summary>When the change happened in simulation time.</summary>
        public long UtcSeconds { get; }
    }

    /// <summary>The rest period began and the rest lock now holds.</summary>
    public sealed class DuskFell : GameEvent
    {
        public DuskFell(long utcSeconds)
            : base(utcSeconds)
        {
        }
    }

    /// <summary>The rest period ended and a new game day began (GLOSSARY: Gryning, Spelardygn).</summary>
    public sealed class DawnBroke : GameEvent
    {
        public DawnBroke(long utcSeconds, int gameDay)
            : base(utcSeconds) => GameDay = gameDay;

        /// <summary>The game day that began.</summary>
        public int GameDay { get; }
    }

    /// <summary>Self-care was converted into a force; <see cref="Amount"/> is after the soft cap (D-35).</summary>
    public sealed class ForceCredited : GameEvent
    {
        public ForceCredited(long utcSeconds, Force force, int amount)
            : base(utcSeconds)
        {
            Force = force;
            Amount = amount;
        }

        public Force Force { get; }

        public int Amount { get; }
    }

    /// <summary>A correction downwards removed force, clipped at zero (D-36).</summary>
    public sealed class ForceWithdrawn : GameEvent
    {
        public ForceWithdrawn(long utcSeconds, Force force, int amount)
            : base(utcSeconds)
        {
            Force = force;
            Amount = amount;
        }

        public Force Force { get; }

        public int Amount { get; }
    }
}
