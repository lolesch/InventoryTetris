using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using Submodules.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    /// <summary>
    /// The scene's hero: a thin wrapper over a <see cref="Hero"/> built from <see cref="HeroData"/>
    /// (issue #110). Every behaviour - stats, XP, damage, regeneration, the item stats - is the
    /// hero's; this keeps what is not yet: the pick-up, which reaches the containers through
    /// <c>InventoryProvider</c> until the Hero and the World are built in order. The stat sheet is
    /// the <c>CharacterStatPanel</c>'s, bound to the hero's change events (issue #111).
    /// </summary>
    public sealed class LocalPlayer : BaseCharacter, IStatReceiver, IItemReceiver
    {
        [SerializeField, Tooltip("The template the hero is built from.")] private HeroData data;

        // TODO: ATTRIBUTES and DERIVED STATS => define and calculate derived values => see Bone&Blood
        protected override Hero BuildHero() =>
            // Unity's lifetime-aware == : a template that was never assigned.
            data != null ? new Hero(data) : throw new InvalidOperationException($"{name} has no HeroData assigned.");

        protected override void OnDeath() => Debug.LogWarning($"{name.ColoredComponent()} {"DIED!".Colored(Color.red)}", this);

        public void GainExperience(float exp, uint monsterLevel) => Hero.GainExperience(exp, monsterLevel);

        public void AddItemStats(IReadOnlyList<CharacterStatModifier> stats) => Hero.AddItemStats(stats);

        public void RemoveItemStats(IReadOnlyList<CharacterStatModifier> stats) => Hero.RemoveItemStats(stats);

        /// <summary>
        /// The debug spawners' (and the legacy <c>DummyTarget</c>'s) entry: the same placement
        /// as <see cref="PickUpItem(ItemInstance, uint)"/>, plus - in a debug build - an
        /// overflow to the Stash so a spawn burst is not lost to a full bag. Anything that
        /// must treat "no room" as a fact (loot, Buy, Stash retrieval, Corpse recovery) goes
        /// through <see cref="IItemReceiver"/> instead.
        /// </summary>
        public bool PickUpItemOrStash(Package package)
        {
            if (TryAcquire(ref package))
                return true;

            /// Debug try add remaining package amount to player stash
            if (Debug.isDebugBuild)
            {
                Debug.LogWarning($"Trying to add the remaining amount of {package.Amount} to {InventoryProvider.Instance.Stash}");

                return InventoryProvider.Instance.Stash.TryAddToContainer(ref package);
            }

            return false;
        }

        /// <see cref="IItemReceiver"/> takes item+amount apart rather than a <c>Package</c>,
        /// since <c>Package</c> is Containers-resident and IItemReceiver lives in Items -
        /// see the interface doc. This just rewraps into the Package the real logic needs, and
        /// stops at <see cref="ItemAcquisition.TryPlace"/>: no room means <c>false</c>, never
        /// the debug Stash.
        public bool PickUpItem(ItemInstance item, uint amount)
        {
            var package = new Package(null, item, amount);
            return TryAcquire(ref package);
        }

        private static bool TryAcquire(ref Package package) =>
            ItemAcquisition.TryPlace(ref package, InventoryProvider.Instance.Equipment, InventoryProvider.Instance.Inventory);

        public float CompareStatModifiers(CharacterStatModifier playerStatModifier, StatModifier other) => Hero.CompareStatModifiers(playerStatModifier, other);
        public float CompareStatModifiers(StatName stat, StatModifier current, StatModifier other) => Hero.CompareStatModifiers(stat, current, other);
    }
}
