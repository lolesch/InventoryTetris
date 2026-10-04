using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.GUI.Displays;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using Submodules.Utility.Extensions;
using Submodules.Utility.Tools;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    /// <summary>
    /// The scene's hero: a thin wrapper over a <see cref="Hero"/> built from <see cref="HeroData"/>
    /// (issue #110). Every behaviour - stats, XP, damage, regeneration, the item stats - is the
    /// hero's; this keeps what is not yet: the stat display pool (until the stat panel binds to the
    /// hero) and the pick-up, which reaches the containers through <c>InventoryProvider</c> until
    /// the Hero and the World are built in order.
    /// </summary>
    public sealed class LocalPlayer : BaseCharacter, IStatReceiver, IItemReceiver
    {
        [SerializeField, Tooltip("The template the hero is built from.")] private HeroData data;

        //TODO: make the displayLogic its own component and design its layout individually and not via a pool
        [SerializeField] private CharacterStatDisplay characterStatPrefab;
        [SerializeField] private PrefabPool<CharacterStatDisplay> characterStatPool;

        // TODO: ATTRIBUTES and DERIVED STATS => define and calculate derived values => see Bone&Blood
        private void Awake() => characterStatPool = new(characterStatPrefab);

        protected override Hero BuildHero() =>
            // Unity's lifetime-aware == : a template that was never assigned.
            data != null ? new Hero(data) : throw new InvalidOperationException($"{name} has no HeroData assigned.");

        private IEnumerable<CharacterStat> StatsAndResources => Hero.Resources.Cast<CharacterStat>().Union(Hero.Stats);

        private void OnEnable()
        {
            foreach (var stat in StatsAndResources)
            {
                stat.TotalHasChanged -= UpdateStatDisplays;
                stat.TotalHasChanged += UpdateStatDisplays;
            }

            UpdateStatDisplays();
        }

        private void OnDisable()
        {
            foreach (var stat in StatsAndResources)
                stat.TotalHasChanged -= UpdateStatDisplays;
        }

        private void UpdateStatDisplays(float debug = 0)
        {
            characterStatPool.ReleaseAll();

            foreach (var stat in StatsAndResources)
            {
                //TODO: extend prefabPool to support IDisplay<T> that update the Refresh(newData) before activating the object

                var statDisplay = characterStatPool.GetObject(false);

                statDisplay.Refresh(new(stat));

                statDisplay.gameObject.SetActive(true);
            }
        }

        protected override void OnDeath() => Debug.LogWarning($"{name.ColoredComponent()} {"DIED!".Colored(Color.red)}", this);

        public void GainExperience(float exp, uint monsterLevel) => Hero.GainExperience(exp, monsterLevel);

        public void AddItemStats(IReadOnlyList<CharacterStatModifier> stats)
        {
            Hero.AddItemStats(stats);

            UpdateStatDisplays();
        }

        public void RemoveItemStats(IReadOnlyList<CharacterStatModifier> stats)
        {
            Hero.RemoveItemStats(stats);

            UpdateStatDisplays();
        }

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
