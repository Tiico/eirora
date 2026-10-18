using System;
using System.Collections.Generic;
using UnityEngine;

namespace Eirora.Client.Look
{
    /// <summary>
    /// How something on the island looks at each stage of its progression, put together from parts as data so a
    /// designer can change it without code (ADR-0009, NFR-14).
    /// </summary>
    public abstract class StagedLook : ScriptableObject
    {
        [Tooltip("The most triangles any stage may have (art-direction.md, section 3: house 60–200, special building 600, tree 20–60).")]
        public int triangleBudget = 200;

        /// <summary>Raised in the editor after a look or piece changes, so the island and the gallery redraw at once.</summary>
        public static event Action? Edited;

        public abstract IReadOnlyList<LookStage> Stages { get; }

        /// <summary>The parts of a stage after everything it builds on; a stage past the last shows the last.</summary>
        public IReadOnlyList<LookPart> PartsAt(int stage)
        {
            var stages = Stages;
            var parts = new List<LookPart>();
            if (stages.Count == 0)
            {
                return parts;
            }

            int last = Mathf.Clamp(stage, 0, stages.Count - 1);
            int first = last;
            while (first > 0 && stages[first].extendsPrevious)
            {
                first--;
            }

            for (int i = first; i <= last; i++)
            {
                var current = stages[i];
                parts.RemoveAll(p => current.removes.Contains(p.id));
                foreach (var part in current.parts)
                {
                    int same = part.id.Length == 0 ? -1 : parts.FindIndex(p => p.id == part.id);
                    if (same >= 0)
                    {
                        parts[same] = part;
                    }
                    else
                    {
                        parts.Add(part);
                    }
                }
            }

            return parts;
        }

        /// <summary>Draws a stage standing on <paramref name="at"/>; lights go to <paramref name="glows"/>.</summary>
        public void Draw(MeshBuilder surfaces, MeshBuilder glows, int stage, Vector3 at, int seed)
        {
            foreach (var part in PartsAt(stage))
            {
                part.Draw(surfaces, glows, at, seed);
            }
        }

        internal static void RaiseEdited() => Edited?.Invoke();

        protected virtual void OnValidate()
        {
#if UNITY_EDITOR
            // After the inspector is done, and once for many changes in a row.
            UnityEditor.EditorApplication.delayCall -= RaiseEdited;
            UnityEditor.EditorApplication.delayCall += RaiseEdited;
#endif
        }
    }
}
