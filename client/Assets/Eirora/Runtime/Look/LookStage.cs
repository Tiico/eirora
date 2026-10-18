using System;
using System.Collections.Generic;
using UnityEngine;

namespace Eirora.Client.Look
{
    /// <summary>
    /// One step of a look's progression, such as a building's level. A stage builds on the one before it: it keeps
    /// those parts, takes away the ones named in <see cref="removes"/> and adds its own, where a part with the name
    /// of an earlier one replaces it.
    /// </summary>
    [Serializable]
    public class LookStage
    {
        [Tooltip("A name for the designer, shown in the gallery.")]
        public string name = "";

        [Tooltip("Builds on the stage before; off to start from nothing.")]
        public bool extendsPrevious = true;

        [Tooltip("Names of earlier parts this stage takes away.")]
        public List<string> removes = new();

        public List<LookPart> parts = new();
    }

    /// <summary>A stage of nature, shown from a share of fullness: a forest's stock, a field's ripeness.</summary>
    [Serializable]
    public sealed class NatureStage : LookStage
    {
        [Tooltip("Shown from this share of fullness (0–1) until a later stage's From is reached.")]
        [Range(0f, 1f)]
        public float from;
    }
}
