using System.Collections.Generic;
using UnityEngine;

namespace Eirora.Client.Look
{
    /// <summary>How nature looks as it fills up: a forest growing back, a field ripening (GLOSSARY: långsam process).</summary>
    [CreateAssetMenu(menuName = "Eirora/Nature Look", fileName = "NatureLook")]
    public sealed class NatureLook : StagedLook
    {
        public NatureKind kind;

        [Tooltip("Rises above the fog over unexplored land, as mountains do.")]
        public bool visibleInFog;

        [Tooltip("Stages in order of From.")]
        public List<NatureStage> stages = new();

        public override IReadOnlyList<LookStage> Stages => stages;

        /// <summary>The stage with the highest From that <paramref name="fullness"/> (0–1) has reached; the first if none has.</summary>
        public int StageOf(float fullness)
        {
            int best = 0;
            float reached = float.NegativeInfinity;
            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i].from <= fullness && stages[i].from >= reached)
                {
                    best = i;
                    reached = stages[i].from;
                }
            }

            return best;
        }
    }
}
