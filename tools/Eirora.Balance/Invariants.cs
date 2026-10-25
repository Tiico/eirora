using Eirora.Simulation;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;

namespace Eirora.Balance;

/// <summary>
/// Checks every hour that nothing decays and nothing leaves its bounds: villagers, housing, buildings, levels, roads
/// and known tiles never decrease (INV-V1), every store holds between nothing and its capacity (INV-R1), and forces
/// are never negative (INV-F1) and never decrease with time alone (INV-F6).
/// </summary>
/// <remarks>
/// Only commands spend forces and demolish buildings (D-38), and the bots send them at dawn, between two checks;
/// <see cref="AfterCommands"/> checks that the commands lowered nothing but the buildings they demolished, and takes the
/// forces and the measures as they stand after them, so that the next check sees only what time did.
/// </remarks>
internal sealed class Invariants
{
    /// <summary>What each storehouse holds of each resource (economy-v1.md, section 2).</summary>
    public const int StoragePerDepot = 150;

    /// <summary>What each jetty holds of each resource for the ships (economy-v2.md, section 6).</summary>
    public const int StorageAtJetty = 50;

    private static readonly string[] s_monotone = ["villagers", "housing", "buildings", "levels", "roads", "known tiles"];
    private static readonly Force[] s_forces = [Force.Exploration, Force.BuildPower, Force.DawnPower];

    private long[] _previous;
    private int[] _forces;
    private Dictionary<Hex, (BuildingKind Kind, int Level)> _built;

    public Invariants(World world)
    {
        _previous = Monotone(world);
        _forces = Forces(world);
        _built = Built(world);
    }

    /// <summary>Hours checked so far.</summary>
    public int Hours { get; private set; }

    /// <summary>
    /// Checks that the commands just sent lowered nothing but the buildings demolished on <paramref name="demolished"/>,
    /// with their levels and housing (INV-V1, D-38), and takes the forces and the measures as they left them.
    /// </summary>
    /// <exception cref="InvariantViolationException">A command lowered something it did not demolish.</exception>
    public void AfterCommands(World world, int day, IReadOnlyCollection<Hex> demolished)
    {
        long[] excused = new long[s_monotone.Length];
        foreach (var (tile, (kind, level)) in _built)
        {
            if (demolished.Contains(tile))
            {
                excused[1] += world.Content.For(kind).Housing;
                excused[2]++;
                excused[3] += level;
            }
            else if (world.BuildingAt(tile) is not { } now || now.Kind != kind || now.Level < level)
            {
                throw new InvariantViolationException($"Game day {day}: the {kind} on {tile} was lowered by a command that did not demolish it (INV-V1).");
            }
        }

        long[] after = Monotone(world);
        for (int i = 0; i < after.Length; i++)
        {
            if (after[i] < _previous[i] - excused[i])
            {
                throw new InvariantViolationException($"Game day {day}: {s_monotone[i]} fell from {_previous[i]} to {after[i]} with the commands (INV-V1).");
            }
        }

        _forces = Forces(world);
        _previous = after;
        _built = Built(world);
    }

    /// <exception cref="InvariantViolationException">Something decayed or left its bounds.</exception>
    public void Check(World world, int day)
    {
        long[] now = Monotone(world);
        for (int i = 0; i < now.Length; i++)
        {
            if (now[i] < _previous[i])
            {
                throw new InvariantViolationException($"Game day {day}: {s_monotone[i]} fell from {_previous[i]} to {now[i]} (INV-V1).");
            }
        }

        foreach (var island in world.Islands)
        {
            long capacity = Capacity(world, island);
            foreach (var resource in Enum.GetValues<Resource>())
            {
                long stock = world.Stock(island, resource);
                if (stock < 0 || stock > capacity)
                {
                    throw new InvariantViolationException($"Game day {day}: {resource} on {island} is {stock}, outside 0–{capacity} (INV-R1).");
                }
            }
        }

        int[] forces = Forces(world);
        for (int i = 0; i < forces.Length; i++)
        {
            if (forces[i] < 0)
            {
                throw new InvariantViolationException($"Game day {day}: {s_forces[i]} is {forces[i]} (INV-F1).");
            }

            if (forces[i] < _forces[i])
            {
                throw new InvariantViolationException($"Game day {day}: {s_forces[i]} fell from {_forces[i]} to {forces[i]} without a command (INV-F6).");
            }
        }

        _previous = now;
        _forces = forces;
        _built = Built(world);
        Hours++;
    }

    /// <summary>
    /// What a store on an island can hold; a world starts with a storehouse on the home island, so its starting stock
    /// always fits.
    /// </summary>
    private static long Capacity(World world, string island) => world.Buildings.Where(b => b.Island == island).Sum(b => b.Kind switch
    {
        BuildingKind.Storehouse => StoragePerDepot,
        BuildingKind.Jetty => StorageAtJetty,
        _ => 0,
    });

    private static int[] Forces(World world) => [.. s_forces.Select(world.Available)];

    private static Dictionary<Hex, (BuildingKind Kind, int Level)> Built(World world) => world.Buildings.ToDictionary(b => b.Tile, b => (b.Kind, b.Level));

    private static long[] Monotone(World world)
    {
        long known = 0;
        for (int i = 0; i < world.Map.Disc.Count; i++)
        {
            known += world.IsKnown(world.Map.Disc[i]) ? 1 : 0;
        }

        return
        [
            world.Villagers,
            world.Housing,
            world.Buildings.Count,
            world.Buildings.Sum(b => b.Level),
            world.Roads.Count,
            known,
        ];
    }
}

/// <summary>A run broke an invariant: the simulation let something decay or leave its bounds.</summary>
public sealed class InvariantViolationException : Exception
{
    public InvariantViolationException()
    {
    }

    public InvariantViolationException(string message)
        : base(message)
    {
    }

    public InvariantViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
