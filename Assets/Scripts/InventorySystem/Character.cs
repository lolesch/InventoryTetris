using System.Collections.Generic;
using TeppichsTools.Creation;
using TMPro;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data
{
    public class Character : MonoSingleton<Character>
    {
        [SerializeField] private int characterLevel = 0;
        public int CharacterLevel => characterLevel;

        [SerializeField] private PlayerStat[] stats = new PlayerStat[(System.Enum.GetValues(typeof(StatName)) as StatName[]).Length];
        public PlayerStat[] Stats => stats;

        private List<TextMeshProUGUI> mainStatDisplays = new();
        [SerializeField] private TextMeshProUGUI statPrefab;

        // TODO: DERIVED STATS => define and calculate derived values
        // PlayerAttributes[]
        // each PlayerAttribute has a list of PlayerStats to modify
        // defines Attribute conversion

        private void OnValidate()
        {
            var affixes = System.Enum.GetValues(typeof(StatName)) as StatName[];

            if (stats.Length != affixes.Length)
            {
                stats = new PlayerStat[(System.Enum.GetValues(typeof(StatName)) as StatName[]).Length];
                for (var i = 0; i < affixes.Length; i++)
                {
                    stats[i] = new PlayerStat(affixes[i]);
                    // feed in base values from a scriptable object?
                }
            }
        }

        private void OnEnable()
        {
            if (statPrefab != null)
            {
                for (var i = 0; i < stats.Length; i++)
                {
                    var display = Instantiate(statPrefab, statPrefab.transform.parent);
                    display.gameObject.SetActive(true);
                    mainStatDisplays.Add(display);
                    display.text = $"{stats[i].Stat}: {stats[i].ModifiedValue:0.###}";
                }
            }
            UpdateStatDisplays();
        }

        private void UpdateStatDisplays()
        {
            for (var i = 0; i < mainStatDisplays.Count; i++)
                mainStatDisplays[i].text = $"{Stats[i].Stat}: {Stats[i].ModifiedValue:0.###}";
        }

        public void AddItemStats(List<PlayerStatModifier> stats)
        {
            foreach (var itemStat in stats)
                for (var i = 0; i < this.stats.Length; i++)
                    if (this.stats[i].Stat == itemStat.Stat)
                    {
                        this.stats[i].AddModifier(itemStat.Modifier);
                        break;
                    }
            UpdateStatDisplays();
        }

        public void RemoveItemStats(List<PlayerStatModifier> stats)
        {
            foreach (var itemStat in stats)
                for (var i = this.stats.Length; i-- > 0;)
                    if (this.stats[i].Stat == itemStat.Stat)
                    {
                        this.stats[i].RemoveModifier(itemStat.Modifier);
                        break;
                    }
            UpdateStatDisplays();
        }

        private PlayerStat GetStat(StatName stat)
        {
            for (var i = Stats.Length; i-- > 0;)
                if (Stats[i].Stat == stat)
                    return Stats[i];
            return null;
        }

        // make it an interface for all things that have a list of stats on them?
        public float GetStatValue(StatName stat) => GetStat(stat).ModifiedValue;
    }
}
