using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Services;
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
            var simulation = SimulationService.Instance;

            RefreshCombatStats(simulation);
            RefreshLastRun(simulation.Run);
        }

        private void RefreshCombatStats(ISimulationService simulation)
        {
            if (combatStatsText == null) return;

            var encounter = simulation.Run.Encounter;
            if (encounter == null)
            {
                combatStatsText.text = string.Empty;
                return;
            }

            var hero = encounter.Hero;
            var groundDrops = simulation.LootFlow?.Ground.StoredPackages.Count ?? 0;

            combatStatsText.text =
                (simulation.IsPaused ? "PAUSED - Space to resume\n" : string.Empty) +
                (encounter.IsArriving
                    ? $"Arriving…   cleared {encounter.EncountersCleared}\n"
                    : $"Encounter {encounter.CurrentEncounter}   cleared {encounter.EncountersCleared}\n") +
                $"Enemies  alive {encounter.AliveEnemyCount}   defeated {encounter.EnemiesDefeated}\n" +
                $"Hero HP {hero.HealthFraction * 100f:0}%   Resource {hero.ResourceFraction * 100f:0}%\n" +
                $"XP gained {encounter.SettledXp}\n" +
                $"Sim time {encounter.Duration.ToString(NumberFormats.Seconds)}s\n" +
                $"Ground drops {groundDrops}   coins banked {simulation.Run.CurrencyBanked:n0}";
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
                $"XP gained {result.XpSettled}";

            text += result.Outcome == RunOutcome.Died
                ? $"\nfee {result.CurrencyFee:n0}   XP lost {result.XpLost}"
                : $"\nbanked {result.CurrencyBanked:n0}";

            lastRunText.text = text;
        }
    }
}
