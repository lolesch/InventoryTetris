using System.Linq;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Services;
using Submodules.Utility.Extensions;
using Submodules.Utility.UI;
using UnityEngine;
using UnityEngine.UI;
using static ToolSmiths.InventorySystem.GUI.Displays.CharacterStatDisplay;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public sealed class CharacterStatDisplay : MonoBehaviour, IDisplay<CharacterStatData>
    {
        public struct CharacterStatData
        {
            private CharacterStat stat;
            public string displayText;
            public Sprite icon;
            public CharacterStatData(CharacterStat characterStat, Package[] compareTo = null)
            {
                stat = characterStat;

                var statName = stat.Stat.ToDescription();

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
                    : $"({baseValue.ToString(NumberFormats.Stat)} + {flatAddModValue.ToString(NumberFormats.Stat)}) * {percentAddModValue.ToString(NumberFormats.Stat)} {percentMultModString}";

                displayText = $"{stat.TotalValue.ToString(NumberFormats.Stat)}\t{modDetailText.Colored(Color.gray)}"; //{statName}
                icon = ItemService.Instance.GetStatIcon(stat.Stat);
            }
        }

        [SerializeField] private Image icon;

        [SerializeField] private TextMeshProUGUI text;

        public void Refresh(CharacterStatData newData)
        {
            icon.sprite = newData.icon;
            text.text = newData.displayText;
        }
    }
}