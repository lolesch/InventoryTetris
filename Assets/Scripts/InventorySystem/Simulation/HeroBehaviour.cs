using System;
using ToolSmiths.InventorySystem.Data.Enums;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The six slider-fed values that steer the hero through a Run (issue #23; spec
    /// <i>HeroBehaviour</i>), held in the Session save and written by the sliders on their
    /// change event (issue #27) — read live here, never polled by value snapshot.
    ///
    /// <see cref="ShouldRecallForHealth"/> and <see cref="ShouldRecallForBagFull"/> are the pure
    /// predicates the engine-side adapter (issue #26) calls <c>RunState.Recall</c> from.
    /// <see cref="ShouldCast"/> is the <see cref="CastThreshold"/> hysteresis latch (ADR-0010):
    /// hold below the fraction, then Cast every opportunity down to empty, then wait back up —
    /// it never gates the Strike, because the Strike never asks it. <see cref="AdmitsItem"/> and
    /// <see cref="AdmitsCoin"/> are the loot filter, reading a coin denomination's fixed Rarity
    /// off CONTEXT.md's Denomination ladder (iron Common … gold Unique). <see cref="Engagement"/>
    /// and <see cref="SimSpeed"/> are plain storage — the Encounter sim and the clock adapter
    /// (issues #20, #26) read them directly.
    /// </summary>
    public sealed class HeroBehaviour
    {
        // The live sim's Regenerate runs every tick before the next Cast affordability check
        // (EncounterSimulation.Step), so Resource only ever sits at a nonzero remainder and is
        // already climbing again by the time anything samples it — an exact float zero is never
        // actually observed once a casting run is under way. ADR-0010 itself says "burn to
        // ~empty", so the release floor is a near-zero band, not a bit-exact comparison.
        private const float EmptyResourceFraction = 0.02f;

        private bool _castingRun;

        /// <summary>Auto-Recall fires at or below this hero health fraction.</summary>
        public float RetreatHealthFraction { get; set; }

        /// <summary>
        /// Auto-Recall fires at or above this bag fill fraction. Unlike
        /// <see cref="RetreatHealthFraction"/>'s inert zero default, a default (0f)
        /// <see cref="RecallBagFillFraction"/> fires <see cref="ShouldRecallForBagFull"/>
        /// immediately for any fill — the caller (issue #27's slider) must write a real value
        /// before this is read.
        /// </summary>
        public float RecallBagFillFraction { get; set; }

        /// <summary>
        /// The <see cref="ShouldCast"/> hysteresis latch's rising edge — charge Resource to this
        /// fraction to start a casting run.
        /// </summary>
        public float CastThreshold { get; set; }

        /// <summary>The lowest <see cref="ItemRarity"/> <see cref="AdmitsItem"/> / <see cref="AdmitsCoin"/> will pick up.</summary>
        public ItemRarity LootFilterMinimum { get; set; }

        /// <summary>Sim speed multiplier, 1..~8 log-mapped — consumed as <c>clock.Advance(dt * SimSpeed)</c> (issue #26).</summary>
        public float SimSpeed { get; set; }

        /// <summary>The spawn-refill target the Encounter sim (issue #20) reads every spawn tick.</summary>
        public int Engagement { get; set; }

        /// <summary>Auto-Recall trigger: the hero's health has dropped to or below <see cref="RetreatHealthFraction"/>.</summary>
        public bool ShouldRecallForHealth(float healthFraction) => healthFraction <= RetreatHealthFraction;

        /// <summary>Auto-Recall trigger: the bag has filled to or above <see cref="RecallBagFillFraction"/>.</summary>
        public bool ShouldRecallForBagFull(float bagFillFraction) => bagFillFraction >= RecallBagFillFraction;

        /// <summary>
        /// The <see cref="CastThreshold"/> hysteresis latch. Starts a casting run once
        /// <paramref name="resourceFraction"/> charges up to the threshold; once running, stays
        /// <c>true</c> regardless of the fraction dropping back below it, releasing only once the
        /// pool is spent down to <see cref="EmptyResourceFraction"/> — the run then has to
        /// recharge to the threshold again to restart.
        /// </summary>
        public bool ShouldCast(float resourceFraction)
        {
            if (!_castingRun && resourceFraction >= CastThreshold)
                _castingRun = true;
            else if (_castingRun && resourceFraction <= EmptyResourceFraction)
                _castingRun = false;

            return _castingRun;
        }

        /// <summary>The loot filter for items: admits <paramref name="rarity"/> at or above <see cref="LootFilterMinimum"/>.</summary>
        public bool AdmitsItem(ItemRarity rarity) => rarity >= LootFilterMinimum;

        /// <summary>The loot filter for coins: admits <paramref name="denomination"/> by its fixed Rarity on CONTEXT.md's ladder.</summary>
        public bool AdmitsCoin(CurrencyType denomination) => RarityOf(denomination) >= LootFilterMinimum;

        // CONTEXT.md's Denomination entry: "the ladder is iron -5-> copper -12-> silver -20->
        // gold. Each rung carries a fixed Rarity — iron Common, copper Magic, silver Rare, gold
        // Unique — so a loot filter reads coins and items on one scale."
        private static ItemRarity RarityOf(CurrencyType denomination) => denomination switch
        {
            CurrencyType.Iron => ItemRarity.Common,
            CurrencyType.Copper => ItemRarity.Magic,
            CurrencyType.Silver => ItemRarity.Rare,
            CurrencyType.Gold => ItemRarity.Unique,
            _ => ItemRarity.NoDrop,
        };

        // ─── slider mapping helpers (issue #27) ─────────────────────────────

        /// <summary>
        /// The discrete <see cref="ItemRarity"/> steps a loot-filter slider can select:
        /// Common, Magic, Rare, Unique. Used by the UI slider and by
        /// <see cref="RarityIndex"/> / <see cref="RarityForIndex"/>.
        /// </summary>
        public static readonly ItemRarity[] RaritySteps =
        {
            ItemRarity.Common,
            ItemRarity.Magic,
            ItemRarity.Rare,
            ItemRarity.Unique,
        };

        /// <summary>
        /// Logarithmic slider mapping: slider position [0,1] → sim speed [1, 8] via
        /// <c>Math.Pow(8, t)</c>. Fine control at low speeds; no pause position.
        /// </summary>
        public static float SliderToSimSpeed(float sliderValue) =>
            (float)Math.Pow(8.0, Math.Clamp(sliderValue, 0f, 1f));

        /// <summary>Inverse of <see cref="SliderToSimSpeed"/>: sim speed → normalized slider position.</summary>
        public static float SimSpeedToSlider(float simSpeed) =>
            (float)Math.Log(Math.Max(1f, simSpeed), 8.0);

        /// <summary>Map an <see cref="ItemRarity"/> to its index in <see cref="RaritySteps"/>.</summary>
        public static int RarityIndex(ItemRarity rarity)
        {
            for (var i = 0; i < RaritySteps.Length; i++)
                if (rarity <= RaritySteps[i])
                    return i;
            return RaritySteps.Length - 1;
        }

        /// <summary>Map a slider index (0..3) back to the corresponding <see cref="ItemRarity"/>.</summary>
        public static ItemRarity RarityForIndex(int index) =>
            RaritySteps[Math.Clamp(index, 0, RaritySteps.Length - 1)];
    }
}
