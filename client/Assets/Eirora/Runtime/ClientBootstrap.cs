using Eirora.Simulation;
using UnityEngine;

namespace Eirora.Client
{
    /// <summary>
    /// Entry point of the client. For now it only proves on a device that the shared simulation runs inside the build.
    /// </summary>
    public sealed class ClientBootstrap : MonoBehaviour
    {
        private GUIStyle? _titleStyle;
        private GUIStyle? _detailStyle;

        private void OnGUI()
        {
            var scale = Screen.height / 1080f;
            _titleStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _detailStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            _titleStyle.fontSize = Mathf.RoundToInt(96 * scale);
            _detailStyle.fontSize = Mathf.RoundToInt(32 * scale);

            var center = Screen.height / 2f;
            GUI.Label(new Rect(0, center - (120 * scale), Screen.width, 120 * scale), "Eirora", _titleStyle);
            GUI.Label(new Rect(0, center + (10 * scale), Screen.width, 50 * scale), $"Regelversion {RulesVersion.Current}", _detailStyle);
        }
    }
}
