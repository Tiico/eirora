using System.Collections.Generic;
using Eirora.Simulation.Economy;
using UnityEngine;

namespace Eirora.Client.Look
{
    /// <summary>How a building looks at each level: the first stage is level 1, the next level 2, and so on.</summary>
    [CreateAssetMenu(menuName = "Eirora/Building Look", fileName = "BuildingLook")]
    public sealed class BuildingLook : StagedLook
    {
        public BuildingKind kind;

        [Tooltip("One stage per level, from level 1.")]
        public List<LookStage> levels = new();

        public override IReadOnlyList<LookStage> Stages => levels;

        public static int StageOf(int level) => level - 1;
    }
}
