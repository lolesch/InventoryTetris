using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Data.Items;
using ToolSmiths.InventorySystem.Data.Skills;
using ToolSmiths.InventorySystem.Runtime.Provider;
using ToolSmiths.InventorySystem.Utility;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    public class LocalPlayer : BaseCharacter
    {
        // TODO: ATTRIBUTES and DERIVED STATS => define and calculate derived values => see Bone&Blood

        [field: SerializeField] public Skill[] ActiveSkills { get; private set; } = new Skill[6];

        private void Start()
        {
            for (var i = 0; i < ActiveSkills.Length; i++)
            {
                var skill = ActiveSkills[i];
                if (skill == null)
                    continue;

                skill.SpawnData.CooldownTicker = new Ticker(skill.SpawnData.CooldownDuration, true);
            }
        }

        protected override void OnDeath()
        {
            Debug.LogWarning($"{name.ColoredComponent()} {"DIED!".Colored(Color.red)}", this);
            Debug.Break();

            // if(!hardcoreCharacter)
            //  Respawn();
            // else
            //  show death cause and 'createNewCharacter' menu
        }

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
        }

        public void RemoveItemStats(List<CharacterStatModifier> stats)
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
            if (current.Value == other.Value)
                return 0;

            var currentStat = this.GetStat(stat);
            var difference = currentStat.CompareModifiers(current, other);

            if (current.Type == StatModifierType.PercentAdd)
                difference *= 100;
            if (current.Type == StatModifierType.PercentMult)
                difference *= 100;

            return difference;
        }
    }
}
