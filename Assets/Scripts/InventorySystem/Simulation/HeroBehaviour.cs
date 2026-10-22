using System;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The slider-fed values that steer the hero through a Run, held in the Session save and written by the sliders
    /// on their change event. Read live, never polled by value snapshot, so a slider moved mid-Run takes effect at
    /// once. The hero never leaves a Run on his own (ADR-0021).
    /// </summary>
    public sealed class HeroBehaviour
    {
        private bool _castingRun;

        /// <summary>
        /// The <see cref="ShouldCast"/> hysteresis latch's rising edge — charge Resource to this
        /// fraction to start a casting run.
        /// </summary>
        public float CastThreshold { get; set; }

        /// <summary>The lowest <see cref="ItemRarity"/> <see cref="AdmitsItem"/> / <see cref="AdmitsCoin"/> will pick up.</summary>
        public ItemRarity LootFilterMinimum { get; set; }

        /// <summary>
        /// A debug switch (issue #63): on, a kill's item Drops that pass <see cref="AdmitsItem"/> are
        /// picked up on the hero's behalf, as before the Ground Items List; off - the default - every
        /// item Drop lies on the ground until the player clicks it. A coin Pile follows the same rule
        /// with <see cref="AdmitsCoin"/> as its filter (issue #218).
        /// </summary>
        public bool AutoPickup { get; set; }

        /// <summary>The item types <see cref="AdmitsKind"/> will pick up; every type until a toggle switches one off.</summary>
        public LootKind AdmittedKinds { get; set; } = LootKind.All;

        /// <summary>
        /// How strongly home pulls against nearness when the hero picks a target, 0..1 (spatial-combat spec):
        /// 0 takes the enemy nearest himself (a brawler who fights what is in front of him), 1 the one nearest the
        /// arena's origin (a homebody who never wanders). Read live, each time he has no target.
        /// </summary>
        public float OriginWeight { get; set; } = DefaultOriginWeight;

        /// <summary>The slider's starting position: home and nearness pull equally.</summary>
        public const float DefaultOriginWeight = 0.5f;

        /// <summary>Sim speed multiplier, 1..~8 log-mapped — consumed as <c>clock.Advance(dt * SimSpeed)</c> (issue #26).</summary>
        public float SimSpeed { get; set; }

        /// <summary>The spawn-refill target the Encounter sim (issue #20) reads every spawn tick.</summary>
        public int Engagement { get; set; }

        /// <summary>
        /// Starts a casting run once <paramref name="resourceFraction"/> charges up to the threshold;
        /// a started run stays <c>true</c> however far the fraction falls, until it is released.
        /// </summary>
        public bool ShouldCast(float resourceFraction)
        {
            if (!_castingRun && resourceFraction >= CastThreshold)
                _castingRun = true;

            return _castingRun;
        }

        /// <summary>
        /// Ends the run: the pool is drained, so it has to recharge to the threshold to restart.
        /// </summary>
        public void ReleaseCastingRun() => _castingRun = false;

        /// <summary>The loot filter for items: admits <paramref name="rarity"/> at or above <see cref="LootFilterMinimum"/>.</summary>
        public bool AdmitsItem(ItemRarity rarity) => rarity >= LootFilterMinimum;

        /// <summary>The loot filter's type half: admits a <paramref name="kind"/> that is switched on. A <see cref="LootKind.None"/> item has no toggle, so it is never filtered out.</summary>
        public bool AdmitsKind(LootKind kind) => kind == LootKind.None || (AdmittedKinds & kind) != 0;

        /// <summary>The loot filter for coins: admits <paramref name="denomination"/> by its fixed Rarity on GLOSSARY.md's ladder, while the Currency type is on.</summary>
        public bool AdmitsCoin(CurrencyType denomination) =>
            Currency.RarityOf(denomination) >= LootFilterMinimum && AdmitsKind(LootKind.Currency);

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
