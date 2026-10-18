using System.Collections.Generic;
using Eirora.Simulation.Economy;
using UnityEngine;

namespace Eirora.Client.Look
{
    /// <summary>
    /// Every look in Resources/Looks, found by what it shows. Edits to a look show at once, also while playing in the
    /// editor, since the island draws from the assets themselves.
    /// </summary>
    public sealed class Looks
    {
        public const string Folder = "Looks";

        private readonly Dictionary<BuildingKind, BuildingLook> _buildings = new();
        private readonly Dictionary<NatureKind, NatureLook> _nature = new();

        public Looks(IEnumerable<BuildingLook> buildings, IEnumerable<NatureLook> nature)
        {
            foreach (var look in buildings)
            {
                _buildings.TryAdd(look.kind, look);
            }

            foreach (var look in nature)
            {
                _nature.TryAdd(look.kind, look);
            }
        }

        public static Looks Load() => new(Resources.LoadAll<BuildingLook>(Folder), Resources.LoadAll<NatureLook>(Folder));

        public BuildingLook? For(BuildingKind kind) => _buildings.TryGetValue(kind, out var look) ? look : null;

        public NatureLook? For(NatureKind kind) => _nature.TryGetValue(kind, out var look) ? look : null;

        public void Building(MeshBuilder surfaces, MeshBuilder glows, BuildingKind kind, int level, Vector3 at, int seed)
        {
            if (For(kind) is { } look)
            {
                look.Draw(surfaces, glows, BuildingLook.StageOf(level), at, seed);
            }
            else
            {
                Missing(surfaces, at);
            }
        }

        /// <param name="fullness">How full the nature is, 0–1: a forest's stock, a field's ripeness.</param>
        /// <param name="fogged">Whether the hex is still unexplored: only looks that rise above the fog are drawn.</param>
        public void Nature(MeshBuilder surfaces, MeshBuilder glows, NatureKind kind, float fullness, bool fogged, Vector3 at, int seed)
        {
            if (For(kind) is not { } look)
            {
                Missing(surfaces, at);
            }
            else if (!fogged || look.visibleInFog)
            {
                look.Draw(surfaces, glows, look.StageOf(fullness), at, seed);
            }
        }

        private static void Missing(MeshBuilder surfaces, Vector3 at) =>
            surfaces.Box(at, new Vector3(0.35f, 0.35f, 0.35f), 0f, Palette.Missing, Palette.Missing);
    }
}
