using System.Collections.Generic;
using UnityEngine;

namespace Eirora.Client.Look
{
    /// <summary>A reusable piece of the kit, such as a tree, a chimney or a sheep, placed in looks by Piece parts.</summary>
    [CreateAssetMenu(menuName = "Eirora/Look Piece", fileName = "LookPiece")]
    public sealed class LookPiece : ScriptableObject
    {
        [Tooltip("Parts around the piece's own origin, which stands where the Piece part places it.")]
        public List<LookPart> parts = new();

        private void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= StagedLook.RaiseEdited;
            UnityEditor.EditorApplication.delayCall += StagedLook.RaiseEdited;
#endif
        }
    }
}
