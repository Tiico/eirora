using System;
using System.Collections.Generic;
using Eirora.Simulation.Map;

namespace Eirora.WorldGeneration
{
    /// <summary>
    /// Makes a map from a seed (GLOSSARY: Generator). A generator version is locked once worlds are made with it, and
    /// its code then never changes: a new version is a new generator, used only for new worlds (ADR-0010).
    /// </summary>
    public interface IWorldGenerator
    {
        string Id { get; }

        int Version { get; }

        MapSource Source { get; }

        WorldMap Generate(ulong seed);
    }

    /// <summary>A generator that draws again until the validator passes its map, and says how many attempts it took.</summary>
    public interface ICountsAttempts
    {
        WorldMap Generate(ulong seed, out int attempts);
    }

    /// <summary>Every generator version there is; the golden maps in CI cover each of them (ADR-0010).</summary>
    public static class WorldGenerators
    {
        public static IReadOnlyList<IWorldGenerator> All { get; } = new IWorldGenerator[]
        {
            new PrototypeHomeGenerator(),
            new ProceduralArchipelagoV1(),
        };

        /// <summary>The generator with this id and version.</summary>
        /// <exception cref="ArgumentException">There is no such generator in this build.</exception>
        public static IWorldGenerator Get(string id, int version)
        {
            foreach (var generator in All)
            {
                if (generator.Id == id && generator.Version == version)
                {
                    return generator;
                }
            }

            throw new ArgumentException($"There is no generator {id} version {version}.", nameof(id));
        }
    }

    /// <summary>The prototype's handmade map as a generator, so that it has golden maps like any other; the seed is ignored.</summary>
    internal sealed class PrototypeHomeGenerator : IWorldGenerator
    {
        public string Id => PrototypeHome.GeneratorId;

        public int Version => PrototypeHome.GeneratorVersion;

        public MapSource Source => MapSource.Handmade;

        public WorldMap Generate(ulong seed) => PrototypeHome.Map;
    }
}
