using System.Collections.Generic;
using TeppichsTools.Creation;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data
{
    public class Character : MonoSingleton<Character>
    {
        // TODO: decide if list or arrey based on the usecases
        [SerializeField] private List<PlayerStat> mainStats = new List<PlayerStat>();

        //[SerializeField] private PlayerStat[] derivedStats = new PlayerStat[0];
        // TODO: define and calculate derived values

        private void OnValidate()
        {
            var stats = System.Enum.GetValues(typeof(StatName)) as StatName[];

            if (mainStats.Count != stats.Length)
            {
                mainStats.Clear();
                for (int iStats = 0; iStats < stats.Length; iStats++)
                {
                    mainStats.Add(new PlayerStat(stats[iStats]));
                    // feed in base values from a scriptable object?
                }
            }
        }

        public void AddItemStats(List<ItemStat> stats)
        {
            foreach (var itemStat in stats)
                for (int i = 0; i < mainStats.Count; i++)
                    if (mainStats[i].Stat == itemStat.Stat)
                    {
                        mainStats[i].AddModifier(itemStat.Modifier);
                        break;
                    }
        }

        public void RemoveItemStats(List<ItemStat> stats)
        {
            foreach (var itemStat in stats)
                for (int i = mainStats.Count; i-- > 0;)
                    if (mainStats[i].Stat == itemStat.Stat)
                    {
                        mainStats[i].RemoveModifier(itemStat.Modifier);
                        break;
                    }
        }

        /// ContainerDisplayLogic
        //   foreach (var package in storedPackages)
        //   {
        //       foreach (var stat in package.Value.Item.stats)
        //       {
        //           // TODO:
        //           // find the corresponding stat and add it to the modifier list
        //           // sort the list by modifierType and apply the mods to the base value
        //           // then display this value in a UI Reference
        //       }
        //   }
    }
}
