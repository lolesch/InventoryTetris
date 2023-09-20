using System.Collections.Generic;
using System.Linq;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    public class LocalPlayer : BaseCharacter
    {
        // TODO: move that into a display component instead of the actual character
        private List<TextMeshProUGUI> mainStatDisplays = new();
        [SerializeField] private TextMeshProUGUI statPrefab;

        // TODO: ATTRIBUTES and DERIVED STATS => define and calculate derived values => see Bone&Blood

        private void OnEnable()
        {
            if (statPrefab != null)
            {
                var statsAndResources = CharacterStats.Union(CharacterResources);
                for (var i = 0; i < statsAndResources.Count(); i++)
                {
                    var display = Instantiate(statPrefab, statPrefab.transform.parent);
                    display.gameObject.SetActive(true);
                    mainStatDisplays.Add(display);
                }
            }
            UpdateStatDisplays();
        }

        private void UpdateStatDisplays()
        {
            var statsAndResources = CharacterResources.Union(CharacterStats).ToArray();
            for (var i = 0; i < mainStatDisplays.Count; i++)
                mainStatDisplays[i].text = $"{statsAndResources[i].ToString()}";
        }

        protected override void OnDeath() => Debug.LogWarning($"{name.ColoredComponent()} {"died!".Colored(Color.red)}", this);

        public void GainExperience(float exp)
        {
            var experience = this.GetResource(StatName.Experience);

            while (0 < exp)
            {
                exp = experience.AddToCurrent(exp);

                if (experience.IsFull)
                    CharacterLevel = LevelUp(experience);
            }
        }

        private uint LevelUp(CharacterResource experience)
        {
            var nextLevel = CharacterLevel + 1;

            var statMod = new StatModifier(new Vector2Int(0, int.MaxValue), nextLevel * 100 + 80);

            experience.AddModifier(statMod);
            experience.DepleteCurrent();

            return nextLevel;
        }

        public void AddItemStats(List<CharacterStatModifier> stats)
        {
            var resources = new List<CharacterStatModifier>();

            foreach (var itemStat in stats)
                if (itemStat.Stat is StatName.Health or StatName.Resource)
                    resources.Add(itemStat);
                else
                    for (var i = 0; i < CharacterStats.Length; i++)
                        if (CharacterStats[i].Stat == itemStat.Stat)
                        {
                            CharacterStats[i].AddModifier(itemStat.Modifier);
                            break;
                        }

            foreach (var itemStat in resources)
                for (var i = 0; i < CharacterResources.Length; i++)
                    if (CharacterResources[i].Stat == itemStat.Stat)
                    {
                        CharacterResources[i].AddModifier(itemStat.Modifier);
                        break;
                    }

            UpdateStatDisplays();
        }

        public void RemoveItemStats(List<CharacterStatModifier> stats)
        {
            var resources = new List<CharacterStatModifier>();

            foreach (var itemStat in stats)
                if (itemStat.Stat is StatName.Health or StatName.Resource)
                    resources.Add(itemStat);
                else
                    for (var i = CharacterStats.Length; i-- > 0;)
                        if (CharacterStats[i].Stat == itemStat.Stat)
                        {
                            CharacterStats[i].RemoveModifier(itemStat.Modifier);
                            break;
                        }

            foreach (var itemStat in resources)
                for (var i = CharacterResources.Length; i-- > 0;)
                    if (CharacterResources[i].Stat == itemStat.Stat)
                    {
                        CharacterResources[i].RemoveModifier(itemStat.Modifier);
                        break;
                    }

            UpdateStatDisplays();
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
