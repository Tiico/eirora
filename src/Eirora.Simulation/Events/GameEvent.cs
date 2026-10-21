using System.Collections.Generic;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;

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

    /// <summary>Villagers moved in because they were fed and there was room (economy-v1.md, section 4).</summary>
    public sealed class VillagersMovedIn : GameEvent
    {
        public VillagersMovedIn(long utcSeconds, int count)
            : base(utcSeconds) => Count = count;

        public int Count { get; }
    }

    /// <summary>A building, upgrade or road in the queue was finished.</summary>
    public sealed class ProjectCompleted : GameEvent
    {
        public ProjectCompleted(long utcSeconds, ProjectKind kind, BuildingKind building, Hex tile)
            : base(utcSeconds)
        {
            Kind = kind;
            Building = building;
            Tile = tile;
        }

        public ProjectKind Kind { get; }

        public BuildingKind Building { get; }

        /// <summary>The building site, or the building the upgrade or road was for.</summary>
        public Hex Tile { get; }
    }

    /// <summary>Surveying lifted the fog from these tiles (MAP-04).</summary>
    public sealed class TilesRevealed : GameEvent
    {
        public TilesRevealed(long utcSeconds, IReadOnlyList<Hex> tiles)
            : base(utcSeconds) => Tiles = tiles;

        public IReadOnlyList<Hex> Tiles { get; }
    }

    /// <summary>The fog lifted from one tile, paid with Exploration (TASK-032, MAP-04).</summary>
    public sealed class FogLifted : GameEvent
    {
        public FogLifted(long utcSeconds, int q, int r)
            : base(utcSeconds)
        {
            Q = q;
            R = r;
        }

        public int Q { get; }

        public int R { get; }
    }

    /// <summary>The expedition came home and found an island across the sea (EXP-01, EXP-02).</summary>
    public sealed class ExpeditionReturned : GameEvent
    {
        public ExpeditionReturned(long utcSeconds, string island)
            : base(utcSeconds) => Island = island;

        /// <summary>The map's id of the island it found.</summary>
        public string Island { get; }
    }
}
