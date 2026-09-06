using System.Linq;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The "minimal debug Send / Recall control" (issue #43) — a throwaway <see cref="OnGUI"/>
    /// panel that drives the play-mode smoke gate: pick a Location and Send, watch the globe and
    /// enemy counts move, Recall to end the Run. The polished map / sliders / panel-visibility UI
    /// is issue #27, which deletes this.
    ///
    /// In the editor the Location list auto-fills from every authored <see cref="LocationConfig"/>
    /// asset, so a bare scene with just the providers is enough to smoke-test.
    /// </summary>
    public sealed class SimulationDebugPanel : MonoBehaviour
    {
        [SerializeField] private bool show = true;
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

            var encounter = run.Encounter;
            if (encounter != null)
            {
                var hero = encounter.Hero;
                GUILayout.Label($"Encounter {encounter.CurrentEncounter}   cleared {encounter.EncountersCleared}");
                GUILayout.Label($"Enemies  alive {encounter.AliveEnemyCount}   defeated {encounter.EnemiesDefeated}");
                GUILayout.Label($"Hero HP {hero.HealthFraction * 100f:0}%   Resource {hero.ResourceFraction * 100f:0}%");
                GUILayout.Label($"XP pot {encounter.UnsettledXp:0}   settled {encounter.SettledXp}");
                GUILayout.Label($"Sim time {encounter.Duration:0.0}s");

                var loot = provider.LootFlow;
                if (loot != null)
                    GUILayout.Label($"Ground drops {loot.GroundDrops.Count}   coins banked {run.CurrencyBanked}");
            }

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
                GUILayout.Label("Hero down — returning to Town…");
            }
            else if (GUILayout.Button("Recall"))
            {
                provider.Recall();
            }

            if (run.LastResult.HasValue)
            {
                var result = run.LastResult.Value;
                GUILayout.Space(6);
                GUILayout.Label($"Last Run: {result.Outcome}");
                GUILayout.Label($"  kills {result.EnemiesDefeated}   cleared {result.EncountersCleared}");
                GUILayout.Label($"  XP settled {result.XpSettled}   forfeited {result.XpForfeited}");
                if (result.Outcome == RunOutcome.Died)
                    GUILayout.Label($"  fee {result.CurrencyFee:n0}   XP lost {result.XpLost}");
                else
                    GUILayout.Label($"  banked {result.CurrencyBanked:n0}");
            }

            GUILayout.EndArea();
        }

        private static string LabelFor(LocationConfig location) =>
            string.IsNullOrWhiteSpace(location.DisplayName) ? location.name : location.DisplayName;
    }
}
