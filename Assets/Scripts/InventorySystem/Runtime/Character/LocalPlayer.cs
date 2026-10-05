using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Services;
using Submodules.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    /// <summary>
    /// The scene's hero: a thin wrapper over the <see cref="Hero"/> the Session was booted with
    /// (issues #110, #112). Every behaviour - stats, XP, damage, regeneration, the item stats and
    /// the pick-up - is the hero's; this keeps the scene face. The acquisition entry point with
    /// the debug Stash overflow is the inventory service's (<see cref="IInventoryService.PickUpOrStash"/>).
    /// The stat sheet is the <c>CharacterStatPanel</c>'s, bound to the
    /// hero's change events (issue #111).
    /// </summary>
    public sealed class LocalPlayer : BaseCharacter, IStatReceiver, IItemReceiver
    {
        // Read off the Session on every ask, never cached: the Session is rebuilt on each Play entry, and
        // with domain and scene reload disabled this component survives Stop, so a cached hero would
        // be last session's and no longer the one the containers apply their stats to (#114 swaps it
        // on a hero load the same way).
        public override Hero Hero => Session.Instance.Hero;

        // TODO: ATTRIBUTES and DERIVED STATS => define and calculate derived values => see Bone&Blood
        protected override Hero BuildHero() => Session.Instance.Hero;

        protected override void OnDeath() => Debug.LogWarning($"{name.ColoredComponent()} {"DIED!".Colored(Color.red)}", this);

        public void GainExperience(float exp, uint monsterLevel) => Hero.GainExperience(exp, monsterLevel);

        public void AddItemStats(IReadOnlyList<CharacterStatModifier> stats) => Hero.AddItemStats(stats);

        public void RemoveItemStats(IReadOnlyList<CharacterStatModifier> stats) => Hero.RemoveItemStats(stats);

        /// <see cref="IItemReceiver"/> takes item+amount apart rather than a <c>Package</c>,
        /// since <c>Package</c> is Containers-resident and IItemReceiver lives in Items -
        /// see the interface doc. The hero does the placement: no room means <c>false</c>,
        /// never the debug Stash.
        public bool PickUpItem(ItemInstance item, uint amount) => Hero.PickUpItem(item, amount);

        public float CompareStatModifiers(CharacterStatModifier playerStatModifier, StatModifier other) => Hero.CompareStatModifiers(playerStatModifier, other);
        public float CompareStatModifiers(StatName stat, StatModifier current, StatModifier other) => Hero.CompareStatModifiers(stat, current, other);
    }
}
