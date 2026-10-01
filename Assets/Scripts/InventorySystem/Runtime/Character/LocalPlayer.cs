using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.GUI.Displays;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using Submodules.Utility.Extensions;
using Submodules.Utility.Tools;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    public sealed class LocalPlayer : BaseCharacter, IStatReceiver, IItemReceiver
    {
        //TODO: make the displayLogic its own component and design its layout individually and not via a pool
        [SerializeField] private CharacterStatDisplay characterStatPrefab;
        [SerializeField] private PrefabPool<CharacterStatDisplay> characterStatPool;

        // TODO: ATTRIBUTES and DERIVED STATS => define and calculate derived values => see Bone&Blood
        private void Awake() => characterStatPool = new(characterStatPrefab);

        private void OnEnable()
        {
            var statsAndResources = CharacterResources.Union(CharacterStats).ToArray();

            foreach (var stat in statsAndResources)
            {
                stat.TotalHasChanged -= UpdateStatDisplays;
                stat.TotalHasChanged += UpdateStatDisplays;
            }

            UpdateStatDisplays();
        }

        private void OnDisable()
        {
            var statsAndResources = CharacterResources.Union(CharacterStats).ToArray();

            foreach (var stat in statsAndResources)
                stat.TotalHasChanged -= UpdateStatDisplays;
        }

        private void UpdateStatDisplays(float debug = 0)
        {
            var statsAndResources = CharacterResources.Union(CharacterStats).ToArray();

            characterStatPool.ReleaseAll();

            foreach (var stat in statsAndResources)
            {
                //TODO: extend prefabPool to support IDisplay<T> that update the Refresh(newData) before activating the object

                var statDisplay = characterStatPool.GetObject(false);

                statDisplay.Refresh(new(stat));

                statDisplay.gameObject.SetActive(true);
            }
        }

        protected override void OnDeath() => Debug.LogWarning($"{name.ColoredComponent()} {"DIED!".Colored(Color.red)}", this);

        public void GainExperience(float exp, uint monsterLevel)
        {
            if (this.GetResource(StatName.Health).IsDepleted)
                return;

            //TODO: design exp gain 
            var levelDifference = monsterLevel - CharacterLevel;
            var levelBalanceExp = exp * (1f + levelDifference / 100f);
            var experience = this.GetResource(StatName.Experience);

            while (0 < levelBalanceExp)
            {
                levelBalanceExp = experience.AddToCurrent(levelBalanceExp);

                if (experience.IsFull)
                {
                    CharacterLevel++;

                    var statMod = new StatModifier(new Vector2Int(0, int.MaxValue), CharacterLevel * 100 + 80);

                    experience.AddModifier(statMod);
                    experience.DepleteCurrent();

                    CharacterProvider.Instance.HealPlayer();
                }
            }
        }

        public void AddItemStats(IReadOnlyList<CharacterStatModifier> stats)
        {
            var resources = new StatName[] { StatName.Health, StatName.Resource, StatName.Shield, StatName.Experience };

            foreach (var itemStat in stats)
                if (resources.Contains(itemStat.Stat))
                {
                    for (var i = 0; i < CharacterResources.Length; i++)
                        if (CharacterResources[i].Stat == itemStat.Stat)
                        {
                            CharacterResources[i].AddModifier(itemStat.Modifier);
                            break;
                        }
                }
                else
                    for (var i = 0; i < CharacterStats.Length; i++)
                        if (CharacterStats[i].Stat == itemStat.Stat)
                        {
                            CharacterStats[i].AddModifier(itemStat.Modifier);
                            break;
                        }

            UpdateStatDisplays();
        }

        public void RemoveItemStats(IReadOnlyList<CharacterStatModifier> stats)
        {
            var resources = new StatName[] { StatName.Health, StatName.Resource, StatName.Shield, StatName.Experience };
            foreach (var itemStat in stats)
            {
                var couldRemove = false;

                if (resources.Contains(itemStat.Stat))
                {
                    for (var i = CharacterResources.Length; i-- > 0;)
                        if (CharacterResources[i].Stat == itemStat.Stat)
                        {
                            couldRemove = CharacterResources[i].TryRemoveModifier(itemStat.Modifier);
                            break;
                        }
                }
                else
                    for (var i = CharacterStats.Length; i-- > 0;)
                        if (CharacterStats[i].Stat == itemStat.Stat)
                        {
                            couldRemove = CharacterStats[i].TryRemoveModifier(itemStat.Modifier);
                            break;
                        }

                if (!couldRemove)
                    Debug.LogWarning($"could not remove {itemStat.Stat} modifier {itemStat.Modifier}!");
            }

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

        public float CompareStatModifiers(CharacterStatModifier playerStatModifier, StatModifier other) => CompareStatModifiers(playerStatModifier.Stat, playerStatModifier.Modifier, other);
        public float CompareStatModifiers(StatName stat, StatModifier current, StatModifier other)
        {
            var currentStat = this.GetStat(stat);
            var clonedStat = currentStat.GetDeepCopy();
            var clonedStat2 = currentStat.GetDeepCopy();

            if (clonedStat.TryRemoveModifier(current))
                clonedStat.AddModifier(other);

            if (clonedStat2.TryRemoveModifier(other))
                clonedStat2.AddModifier(current);

            return clonedStat2.TotalValue - clonedStat.TotalValue;
            //return currentStat.TotalValue - clonedStat.TotalValue;
        }
    }
}
