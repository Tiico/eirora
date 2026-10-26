using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Eirora.Editor
{
    /// <summary>
    /// Plays the screenshot tour in the editor's play mode, for a machine without a player build of its own: the shots land
    /// where <c>-eirora-screenshots</c> says, as from a player, and the editor exits when the tour is done.
    /// </summary>
    /// <remarks>
    /// Run with: Unity -batchmode -projectPath client -executeMethod Eirora.Editor.EditorTour.Run -eirora-screenshots &lt;folder&gt;.
    /// Entering play mode reloads the domain, so the exit is armed again after the reload from a flag the run sets.
    /// </remarks>
    internal static class EditorTour
    {
        private const string Armed = "Eirora.EditorTour.Armed";

        public static void Run()
        {
            SessionState.SetBool(Armed, true);
            EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void ArmExit()
        {
            if (!SessionState.GetBool(Armed, false))
            {
                return;
            }

            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(Armed, false);
                    Debug.Log("Eirora editor tour: done.");
                    EditorApplication.Exit(0);
                }
            };
        }
    }
}
