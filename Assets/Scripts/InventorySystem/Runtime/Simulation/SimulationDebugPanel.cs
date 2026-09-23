using System.Linq;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// OnGUI debug overlay for the combat context — Send / Recall buttons for manual testing,
    /// plus the Town-only sim-speed/Engagement readout. Auto-populates the Location list from
    /// authored <see cref="LocationConfig"/> assets.
    ///
    /// The live encounter/hero/XP/loot readout and the Last Run summary that used to live here
    /// moved to <see cref="BehaviourSlidersPanel"/> (the Combat Panel), which is player-facing
    /// and already up for the length of a Run — this overlay no longer duplicates them.
    /// </summary>
    public sealed class SimulationDebugPanel : MonoBehaviour
    {
        [SerializeField] private bool show = false;
        [SerializeField] private LocationConfig[] locations = System.Array.Empty<LocationConfig>();

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (locations == null || locations.Length == 0 || locations.All(l => l == null))
                locations = UnityEditor.AssetDatabase.FindAssets("t:LocationConfig")
                    .Select(guid => UnityEditor.AssetDatabase.LoadAssetAtPath<LocationConfig>(
                        UnityEditor.AssetDatabase.GUIDToAssetPath(guid)))
                    .Where(l => l != null)
                    .ToArray();
#endif
        }

        private void OnGUI()
        {
            if (!show) return;

            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            var run = provider.Run;

            GUILayout.BeginArea(new Rect(12, 12, 300, 340), UnityEngine.GUI.skin.box);
            GUILayout.Label($"SIM   Run: {run.Phase}");

            GUILayout.Space(6);

            if (run.Phase == RunPhase.InTown)
            {
                GUILayout.Label($"Sim-speed {provider.Behaviour.SimSpeed:0.0}x   Engagement {provider.Behaviour.Engagement}");
                foreach (var location in locations)
                    if (location != null && GUILayout.Button($"Send  →  {LabelFor(location)}"))
                        provider.Send(location);
            }
            else if (run.HeroIsDown)
            {
                GUILayout.Label("Hero down — returning to Town...");
            }
            else if (GUILayout.Button("Recall"))
            {
                provider.Recall();
            }

            GUILayout.EndArea();
        }

        private static string LabelFor(LocationConfig location) =>
            string.IsNullOrWhiteSpace(location.DisplayName) ? location.name : location.DisplayName;
    }
}
