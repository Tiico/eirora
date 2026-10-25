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

    /// <summary>The rest period ended and a new game day began (GLOSSARY: Gryning, Dag).</summary>
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

    /// <summary>Villagers moved in to a settlement because they were fed and there was room (economy-v1.md, section 4).</summary>
    public sealed class VillagersMovedIn : GameEvent
    {
        public VillagersMovedIn(long utcSeconds, int count, string island)
            : base(utcSeconds)
        {
            Count = count;
            Island = island;
        }

        public int Count { get; }

        /// <summary>The map's id of the settlement's island.</summary>
        public string Island { get; }
    }

    /// <summary>
    /// A free villager moved over a sea route to a settlement whose buildings lacked villagers (D-106; economy-v2.md,
    /// section 6).
    /// </summary>
    public sealed class VillagerMoved : GameEvent
    {
        public VillagerMoved(long utcSeconds, string from, string to)
            : base(utcSeconds)
        {
            From = from;
            To = to;
        }

        /// <summary>The map's id of the island the villager left.</summary>
        public string From { get; }

        /// <summary>The map's id of the island the villager moved to.</summary>
        public string To { get; }
    }

    /// <summary>
    /// The first jetty on an island off the home island was finished, so the island has a settlement of its own, with
    /// housing and no villagers until they move there over the sea route (D-106, D-121).
    /// </summary>
    public sealed class SettlementFounded : GameEvent
    {
        public SettlementFounded(long utcSeconds, string island)
            : base(utcSeconds)
        {
            Island = island;
        }

        /// <summary>The map's id of the settlement's island.</summary>
        public string Island { get; }
    }

    /// <summary>A building, upgrade or road in the queue was finished.</summary>
    public sealed class ProjectCompleted : GameEvent
    {
        public ProjectCompleted(long utcSeconds, ProjectKind kind, BuildingKind? building, Hex tile)
            : base(utcSeconds)
        {
            Kind = kind;
            Building = building;
            Tile = tile;
        }

        public ProjectKind Kind { get; }

        /// <summary>What was built or upgraded; null for a road.</summary>
        public BuildingKind? Building { get; }

        /// <summary>The building site, the building the upgrade was for, or the tile the road was laid on.</summary>
        public Hex Tile { get; }
    }

    /// <summary>The player demolished a building (D-38): the tile is free and half its materials came back (D-108).</summary>
    public sealed class BuildingDemolished : GameEvent
    {
        public BuildingDemolished(long utcSeconds, BuildingKind building, Hex tile)
            : base(utcSeconds)
        {
            Building = building;
            Tile = tile;
        }

        public BuildingKind Building { get; }

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
