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

        [SerializeField] private PlayerStat[] mainStats = new PlayerStat[(System.Enum.GetValues(typeof(StatName)) as StatName[]).Length];
        public PlayerStat[] MainStats => mainStats;

        private List<TextMeshProUGUI> mainStatDisplays = new();
        [SerializeField] private TextMeshProUGUI statPrefab;

        //[SerializeField] private PlayerStat[] derivedStats = new PlayerStat[0];
        // TODO: DERIVED STATS => define and calculate derived values

        private void OnValidate()
        {
            var stats = System.Enum.GetValues(typeof(StatName)) as StatName[];

            if (mainStats.Length != stats.Length)
            {
                mainStats = new PlayerStat[(System.Enum.GetValues(typeof(StatName)) as StatName[]).Length];
                for (var i = 0; i < stats.Length; i++)
                {
                    mainStats[i] = new PlayerStat(stats[i]);
                    // feed in base values from a scriptable object?
                }
            }
        }

        private void OnEnable()
        {
            if (statPrefab != null)
            {
                for (var i = 0; i < mainStats.Length; i++)
                {
                    var display = Instantiate(statPrefab, statPrefab.transform.parent);
                    display.gameObject.SetActive(true);
                    mainStatDisplays.Add(display);
                    display.text = $"{mainStats[i].Stat}: {mainStats[i].ModifiedValue}";
                }
            }
            UpdateStatDisplays();
        }

        private void UpdateStatDisplays()
        {
            for (var i = 0; i < mainStatDisplays.Count; i++)
                mainStatDisplays[i].text = $"{MainStats[i].Stat}: {MainStats[i].ModifiedValue:#.###}";
        }

        public void AddItemStats(List<ItemStat> stats)
        {
            foreach (var itemStat in stats)
                for (var i = 0; i < mainStats.Length; i++)
                    if (mainStats[i].Stat == itemStat.Stat)
                    {
                        mainStats[i].AddModifier(itemStat.Modifier);
                        break;
                    }
            UpdateStatDisplays();
        }

        public void RemoveItemStats(List<ItemStat> stats)
        {
            foreach (var itemStat in stats)
                for (var i = mainStats.Length; i-- > 0;)
                    if (mainStats[i].Stat == itemStat.Stat)
                    {
                        mainStats[i].RemoveModifier(itemStat.Modifier);
                        break;
                    }
            UpdateStatDisplays();
        }
    }
}
