using Submodules.Utility.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>What a hero slot holds.</summary>
    public enum HeroSlotKind
    {
        /// <summary>No hero, and not the next to be made: not usable.</summary>
        Empty,

        /// <summary>The first slot past the last hero: choosing it creates one.</summary>
        Create,

        /// <summary>A saved hero.</summary>
        Hero,
    }

    /// <summary>One slot of the hero slots, resolved by <see cref="HeroSlots.At"/>.</summary>
    public readonly struct HeroSlot
    {
        public HeroSlot(HeroSlotKind kind, HeroSummary hero = default)
        {
            Kind = kind;
            Hero = hero;
        }

        public HeroSlotKind Kind { get; }

        /// <summary>The hero of a <see cref="HeroSlotKind.Hero"/> slot; <c>default</c> for the others.</summary>
        public HeroSummary Hero { get; }

        /// <summary>Whether choosing the slot does anything: a create slot, or a hero whose file reads.
        /// A file that cannot be read is shown but cannot be chosen.</summary>
        public bool IsUsable =>
            Kind == HeroSlotKind.Create
            || Kind == HeroSlotKind.Hero && Hero.Status is LoadStatus.Loaded or LoadStatus.RestoredFromBackup;
    }

    /// <summary>
    /// The fixed slots a hero list is shown in: the heroes in the order they were created, then one slot
    /// to create the next, then empty ones. Creation order, not <see cref="IHeroSaveService.List"/>'s
    /// newest-save-first, so a save never moves a hero to another slot and a new hero takes the create
    /// slot it was made in.
    /// </summary>
    public static class HeroSlots
    {
        /// <summary>The heroes oldest first. Saves written before there was a creation stamp read as the oldest
        /// and keep a fixed order among themselves by id.</summary>
        public static IReadOnlyList<HeroSummary> Ordered(IEnumerable<HeroSummary> heroes) =>
            heroes
                .OrderBy(hero => hero.CreatedAtTicks)
                .ThenBy(hero => hero.Id, StringComparer.Ordinal)
                .ToArray();

        /// <param name="ordered">The result of <see cref="Ordered"/>.</param>
        public static HeroSlot At(IReadOnlyList<HeroSummary> ordered, int slot) =>
            slot < ordered.Count ? new HeroSlot(HeroSlotKind.Hero, ordered[slot])
            : slot == ordered.Count ? new HeroSlot(HeroSlotKind.Create)
            : new HeroSlot(HeroSlotKind.Empty);
    }
}
