using System.Collections.Generic;
using System.Linq;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    public class LocalPlayer : BaseCharacter
    {
        [field: SerializeField, Range(1, 100)] public int CharacterLevel { get; private set; } = 1;

        private List<TextMeshProUGUI> mainStatDisplays = new();
        [SerializeField] private TextMeshProUGUI statPrefab;

        // TODO: DERIVED STATS => define and calculate derived values => see Bone&Blood

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

        public void AddItemStats(List<PlayerStatModifier> stats)
        {
            foreach (var itemStat in stats)
                for (var i = 0; i < CharacterStats.Length; i++)
                    if (CharacterStats[i].Stat == itemStat.Stat)
                    {
                        CharacterStats[i].AddModifier(itemStat.Modifier);
                        break;
                    }
            UpdateStatDisplays();
        }

        public void RemoveItemStats(List<PlayerStatModifier> stats)
        {
            foreach (var itemStat in stats)
                for (var i = CharacterStats.Length; i-- > 0;)
                    if (CharacterStats[i].Stat == itemStat.Stat)
                    {
                        CharacterStats[i].RemoveModifier(itemStat.Modifier);
                        //break;
                    }
            UpdateStatDisplays();
        }

        public float CompareStatModifiers(PlayerStatModifier playerStatModifier, StatModifier other) => CompareStatModifiers(playerStatModifier.Stat, playerStatModifier.Modifier, other);
        public float CompareStatModifiers(StatName stat, StatModifier current, StatModifier other)
        {
            var currentValue = GetStatValue(this, stat);
            var clonedStat = GetStat(this, stat).GetClone();
            clonedStat.RemoveModifier(current);
            clonedStat.AddModifier(other);

            return currentValue - clonedStat.TotalValue;
        }
    }
}