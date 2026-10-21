using UnityEngine;

namespace Eirora.Client.App
{
    /// <summary>
    /// Entry point of the determinism build (TASK-021): the only object in the scene that Builds.AndroidDeterminism
    /// makes for that build alone, so the game's ClientBootstrap is never part of it.
    /// </summary>
    public sealed class DeterminismBootstrap : MonoBehaviour
    {
        private void Start() => DeterminismRun.RunFromResources();
    }
}
