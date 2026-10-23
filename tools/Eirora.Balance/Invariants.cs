using Eirora.Simulation;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Forces;

namespace Eirora.Balance;

/// <summary>
/// Checks every hour that nothing decays and nothing goes negative (INV-V1, INV-R1, INV-F1): villagers, housing,
/// buildings, levels, roads and known tiles never decrease, and no store or force is ever below zero.
/// </summary>
internal sealed class Invariants
{
    private static readonly string[] s_monotone = ["villagers", "housing", "buildings", "levels", "roads", "known tiles"];

    private long[] _previous;

    public Invariants(World world) => _previous = Monotone(world);

    /// <summary>Hours checked so far.</summary>
    public int Hours { get; private set; }

    /// <exception cref="InvariantViolationException">Something decayed or went negative.</exception>
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

        foreach (var island in Enum.GetValues<IslandId>())
        {
            foreach (var resource in Enum.GetValues<Resource>())
            {
                if (world.Stock(island, resource) < 0)
                {
                    throw new InvariantViolationException($"Game day {day}: {resource} on {island} is {world.Stock(island, resource)} (INV-R1).");
                }
            }
        }

        foreach (var force in Enum.GetValues<Force>())
        {
            if (world.Available(force) < 0)
            {
                throw new InvariantViolationException($"Game day {day}: {force} is {world.Available(force)} (INV-F1).");
            }
        }

        _previous = now;
        Hours++;
    }

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
            world.Buildings.Count(b => b.HasRoad),
            known,
        ];
    }
}

/// <summary>A run broke an invariant: the simulation let something decay or go negative.</summary>
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
