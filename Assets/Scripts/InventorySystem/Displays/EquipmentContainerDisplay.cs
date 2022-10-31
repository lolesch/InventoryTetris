using System.Collections.Generic;
using TeppichsTools.Logging;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Displays
{
    [System.Serializable]
    public class EquipmentContainerDisplay : AbstractContainerDisplay
    {
        [System.Serializable]
        private class PlayerStat : ISerializationCallbackReceiver
        {
            // TODO: => Goal Architecture:
            /* characters have an array of mainStats and base values (or unmodified values)
             * and an array of derivedStats and the corresponding calculated values
             * and for each Stat a list of modifiers
             * to get the modified value we go over each modifier sorted by its order
             * and call its Modify() method
             * this value could be stored and recalculated only when modifieres have changed.
             * 
             */

            [HideInInspector]
            [SerializeField] private string name;
            [HideInInspector]
            [SerializeField] private StatName stat;
            [SerializeField] private int baseValue;
            [SerializeField] private List<StatModifier> statModifiers = new List<StatModifier>();
            public StatName Name => stat;

            public PlayerStat(StatName statName, uint baseValue = 0)
            {
                this.stat = statName;
                this.baseValue = (int)baseValue;
            }

            public void OnBeforeSerialize() => name = stat.ToString();

            public void OnAfterDeserialize() => name = stat.ToString();
        }

        [SerializeField] private List<PlayerStat> playerStats = new List<PlayerStat>();

        private void OnValidate()
        {
            var stats = System.Enum.GetValues(typeof(StatName)) as StatName[];

            for (int iStats = 0; iStats < stats.Length; iStats++)
            {
                var contains = false;

                for (int iPlayerStats = 0; iPlayerStats < playerStats.Count; iPlayerStats++)
                    if (playerStats[iPlayerStats].Name == stats[iStats])
                        contains = true;

                if (!contains)
                    playerStats.Add(new PlayerStat(stats[iStats]));
            }
        }

        protected override void SetupSlotDisplays(AbstractDimensionalContainer container)
        {
            foreach (var slotDisplay in containerSlotDisplays)
                slotDisplay.container = Container;
            if (containerSlotDisplays.Count != container.Capacity)
                EditorDebug.LogError($"equipmentSlotDisplays {containerSlotDisplays.Count} of {container.Capacity}");
        }

        protected override void Refresh(Dictionary<Vector2Int, Package> storedPackages)
        {
            base.Refresh(storedPackages);

            foreach (var package in storedPackages)
            {
                foreach (var stat in package.Value.Item.stats)
                {
                    // TODO:
                    // find the corresponding stat and add it to the modifier list
                    // sort the list by modifierType and apply the mods to the base value
                    // then display this value in a UI Reference
                }
            }
        }
    }
}