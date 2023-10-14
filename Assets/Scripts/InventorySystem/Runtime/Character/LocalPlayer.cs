using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.GUI.Displays;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Runtime.Pools;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    public class LocalPlayer : BaseCharacter
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
                //TODO: extend prefabPool to support IDisplay<T> that update the Display(newData) before activating the object

                var statDisplay = characterStatPool.GetObject(false);

                statDisplay.RefreshDisplay(new(stat));

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
                }
            }
        }

        public void AddItemStats(List<CharacterStatModifier> stats)
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

        public void RemoveItemStats(List<CharacterStatModifier> stats)
        {
            var resources = new StatName[] { StatName.Health, StatName.Resource, StatName.Shield, StatName.Experience };

            foreach (var itemStat in stats)
                if (resources.Contains(itemStat.Stat))
                {
                    for (var i = CharacterResources.Length; i-- > 0;)
                        if (CharacterResources[i].Stat == itemStat.Stat)
                        {
                            CharacterResources[i].RemoveModifier(itemStat.Modifier);
                            break;
                        }
                }
                else
                    for (var i = CharacterStats.Length; i-- > 0;)
                        if (CharacterStats[i].Stat == itemStat.Stat)
                        {
                            CharacterStats[i].RemoveModifier(itemStat.Modifier);
                            break;
                        }

            UpdateStatDisplays();
        }

        public bool PickUpItem(Package package)
        {
            if (package.Item is EquipmentItem)
            {
                var equipment = InventoryProvider.Instance.Equipment;

                if (equipment.autoEquip && equipment.AutoEquip(ref package))
                    return true;
            }

            if (InventoryProvider.Instance.Inventory.TryAddToContainer(ref package))
                return true;

            /// Debug try add remaining package amount to player stash
            if (Debug.isDebugBuild)
            {
                Debug.LogWarning($"Trying to add the remaining amount of {package.Amount} to {InventoryProvider.Instance.Stash}");

                return InventoryProvider.Instance.Stash.TryAddToContainer(ref package);
            }

            return false;
        }

        public float CompareStatModifiers(CharacterStatModifier playerStatModifier, StatModifier other) => CompareStatModifiers(playerStatModifier.Stat, playerStatModifier.Modifier, other);
        public float CompareStatModifiers(StatName stat, StatModifier current, StatModifier other)
        {
            var currentStat = this.GetStat(stat);
            var clonedStat = currentStat.GetDeepCopy();
            clonedStat.RemoveModifier(current);
            clonedStat.AddModifier(other);

            return currentStat.TotalValue - clonedStat.TotalValue;
        }
    }
}
