using System.Linq;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;
using UnityEngine.UI;
using static ToolSmiths.InventorySystem.GUI.Displays.CharacterStatDisplay;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public class CharacterStatDisplay : AbstractDisplay<CharacterStatData>
    {
        public struct CharacterStatData
        {
            private CharacterStat stat;
            public string displayText;
            public Sprite icon;
            public CharacterStatData(CharacterStat characterStat, Package[] compareTo = null)
            {
                stat = characterStat;

                var statName = stat.Stat.SplitCamelCase();

                if (statName.Contains("Percent"))
                    statName = statName.Replace(" Percent", "");

                var overwriteMods = stat.StatModifiers.Where(x => x.Type == StatModifierType.Overwrite).OrderByDescending(x => x.Value);

                var baseValue = stat.BaseValue;
                var flatAddModValue = stat.StatModifiers.Where(x => x.Type == StatModifierType.FlatAdd).Sum(x => x.Value);
                var percentAddModValue = 1 + stat.StatModifiers.Where(x => x.Type == StatModifierType.PercentAdd).Sum(x => x.Value / 100);

                var percentMultMods = stat.StatModifiers.Where(x => x.Type == StatModifierType.PercentMult);
                var percentMultModString = "";
                foreach (var mod in percentMultMods)
                    percentMultModString += $"* {1 + mod.Value / 100} "; // add source?

                // TODO: implement statModifier source
                var modDetailText = overwriteMods.Any()
                    ? $"overwritten by: implementStatModSource" //{overwriteMods.FirstOrDefault().Source}"
                    : $"({baseValue:0.##} + {flatAddModValue:0.##}) * {percentAddModValue:0.##} {percentMultModString:0.##}";

                displayText = $"{stat.TotalValue:0.##}\t{modDetailText.Colored(Color.gray)}"; //{statName}
                icon = ItemProvider.Instance.ItemTypeData.GetStatIcon(stat.Stat);
            }
        }

        [SerializeField] protected Image icon;

        [SerializeField] protected TextMeshProUGUI text;

        public override void Display(CharacterStatData newData)
        {
            icon.sprite = newData.icon;
            text.text = newData.displayText;
        }
    }
}