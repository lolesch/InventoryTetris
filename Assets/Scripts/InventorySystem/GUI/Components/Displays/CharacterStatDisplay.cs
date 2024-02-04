using System;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    [Serializable]
    public class CharacterStatDisplay : ShowAltTextOnHover, IDisplay<CharacterStatData>
    {
        [SerializeField] protected Image icon;

        [SerializeField] protected CharacterStat stat;

        private void OnDisable() => stat.TotalHasChanged -= UpdateDisplay;

        private void OnEnable()
        {
            stat.TotalHasChanged -= UpdateDisplay;
            stat.TotalHasChanged += UpdateDisplay;
        }

        public void RefreshDisplay(CharacterStatData newData)
        {
            icon.sprite = newData.icon;
            label.text = newData.displayText;
            stat = newData.stat;

            if (altText == string.Empty)
            {
                var statName = stat.Stat.SplitCamelCase();

                if (statName.Contains("Percent"))
                    statName = statName.Replace(" Percent", "");

                altText = statName;
            }
        }

        public void UpdateDisplay(float ignoreMe = 0) => RefreshDisplay(new(stat));
    }
}