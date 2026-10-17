using System.Collections.Generic;
using Eirora.Simulation.Economy;
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
        public ProjectCompleted(long utcSeconds, ProjectKind kind, BuildingKind building, string siteId)
            : base(utcSeconds)
        {
            Kind = kind;
            Building = building;
            SiteId = siteId;
        }

        public ProjectKind Kind { get; }

        public BuildingKind Building { get; }

        public string SiteId { get; }
    }

    /// <summary>Surveying lifted the fog from these sites (MAP-04).</summary>
    public sealed class SitesRevealed : GameEvent
    {
        public SitesRevealed(long utcSeconds, IReadOnlyList<string> siteIds)
            : base(utcSeconds) => SiteIds = siteIds;

        public IReadOnlyList<string> SiteIds { get; }
    }
}
