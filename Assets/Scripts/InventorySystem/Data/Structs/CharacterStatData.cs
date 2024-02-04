﻿using System.Linq;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Runtime.Provider;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data
{
    public struct CharacterStatData
    {
        public CharacterStat stat;
        public string displayText;
        public Sprite icon;

        public CharacterStatData(CharacterStat Stat, Package[] compareTo = null)
        {
            stat = Stat;
            stat.CalculateTotalValue();

            //var statName = stat.Stat.SplitCamelCase();
            //
            //if (statName.Contains("Percent"))
            //    statName = statName.Replace(" Percent", "");

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
                ? $"{overwriteMods.FirstOrDefault()} overwrite" //{overwriteMods.FirstOrDefault().Source}"
                : $"({baseValue:0.##} + {flatAddModValue:0.##}) * {percentAddModValue:0.##} {percentMultModString:0.##}";

            displayText = $"{stat.TotalValue:0.##}\t{modDetailText.Colored(Color.gray)}"; //{statName}
            icon = ItemProvider.Instance.ItemTypeData.GetStatIcon(stat.Stat);
        }
    }
}