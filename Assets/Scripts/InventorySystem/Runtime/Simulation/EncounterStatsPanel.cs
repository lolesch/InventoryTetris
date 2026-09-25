using ToolSmiths.InventorySystem.Simulation;
using TMPro;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Live encounter stats and last-run summary for the combat panel (issue #61) — the section
    /// between the behaviour sliders and the enemy HP bar pool. Was folded into
    /// <see cref="BehaviourSlidersPanel"/> as a stopgap after <c>SimulationDebugPanel</c> was
    /// removed; moved out to its own sibling here since the two panels' data doesn't otherwise
    /// overlap. Refreshed every <see cref="Update"/> rather than on an event, since HP/XP/sim
    /// time tick continuously while the panel is up.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EncounterStatsPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI combatStatsText;
        [SerializeField] private TextMeshProUGUI lastRunText;

        private void Update()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            RefreshCombatStats(provider);
            RefreshLastRun(provider.Run);
        }

        private void RefreshCombatStats(SimulationProvider provider)
        {
            if (combatStatsText == null) return;

            var encounter = provider.Run.Encounter;
            if (encounter == null)
            {
                combatStatsText.text = string.Empty;
                return;
            }

            var hero = encounter.Hero;
            var groundDrops = provider.LootFlow?.GroundDrops.Count ?? 0;

            combatStatsText.text =
                $"Encounter {encounter.CurrentEncounter}   cleared {encounter.EncountersCleared}\n" +
                $"Enemies  alive {encounter.AliveEnemyCount}   defeated {encounter.EnemiesDefeated}\n" +
                $"Hero HP {hero.HealthFraction * 100f:0}%   Resource {hero.ResourceFraction * 100f:0}%\n" +
                $"XP pot {encounter.UnsettledXp:0}   settled {encounter.SettledXp}\n" +
                $"Sim time {encounter.Duration:0.0}s\n" +
                $"Ground drops {groundDrops}   coins banked {provider.Run.CurrencyBanked:n0}";
        }

        private void RefreshLastRun(RunState run)
        {
            if (lastRunText == null) return;

            if (!run.LastResult.HasValue)
            {
                lastRunText.text = string.Empty;
                return;
            }

            var result = run.LastResult.Value;
            var text =
                $"Last Run: {result.Outcome}\n" +
                $"kills {result.EnemiesDefeated}   cleared {result.EncountersCleared}\n" +
                $"XP settled {result.XpSettled}   forfeited {result.XpForfeited}";

            text += result.Outcome == RunOutcome.Died
                ? $"\nfee {result.CurrencyFee:n0}   XP lost {result.XpLost}"
                : $"\nbanked {result.CurrencyBanked:n0}";

            lastRunText.text = text;
        }
    }
}
