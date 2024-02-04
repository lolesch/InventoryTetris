using System.Collections.Generic;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Provider;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;
using UnityEngine.UI;
using static ToolSmiths.InventorySystem.GUI.Displays.CharacterStatModifierDisplay;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public class CharacterStatModifierDisplay : MonoBehaviour, IDisplay<CharacterStatModifierData>
    {
        public struct CharacterStatModifierData
        {
            private CharacterStatModifier statMod;
            public string displayText;
            public float displayFontSize;
            public Sprite icon;

            public CharacterStatModifierData(CharacterStatModifier characterStatModifier, List<CharacterStatModifier> compareTo)
            {
                statMod = characterStatModifier;
                icon = ItemProvider.Instance.ItemTypeData.GetStatIcon(statMod.Stat);
                displayFontSize = statMod.Modifier.Value.Map(statMod.Modifier.Range, 18, 24);

                var difference = GetStatDifference(statMod, compareTo);

                var comparisonColor = difference < 0 ? Color.red : Color.green;

                var differenceString = difference == 0
                    ? string.Empty
                    : statMod.Modifier.Type switch
                    {
                        StatModifierType.Overwrite => $"={difference:+ 0.###;- 0.###;0.###}",
                        StatModifierType.FlatAdd => $"{difference:+ 0.###;- 0.###;0.###}",
                        StatModifierType.PercentAdd => $"{difference:+ 0.###;- 0.###;0.###}%",
                        StatModifierType.PercentMult => $"*{difference:+ 0.###;- 0.###;0.###}%",

                        _ => $"?? {difference:+ #.###;- #.###;#.###}",
                    };

                displayText = $"{statMod.Modifier} {statMod.Modifier.Range.ToString().Colored(Color.gray)} {differenceString.Colored(comparisonColor)}";

                static float GetStatDifference(CharacterStatModifier stat, List<CharacterStatModifier> compareTo)
                {
                    if (compareTo != null)
                        for (var i = 0; i < compareTo.Count; i++)   // foreach stat of the other item
                            if (compareTo[i].Stat == stat.Stat)     // find a corresponding stat
                                                                    // if (compareTo.Item.Affixes[i].Modifier.Type == stat.Modifier.Type) // find a corresponding mod type
                                return CharacterProvider.Instance.Player.CompareStatModifiers(stat, compareTo[i].Modifier);

                    return stat.Modifier.Value;
                }
            }
        }

        [SerializeField] protected Image icon;

        [SerializeField] protected TextMeshProUGUI text;

        public void RefreshDisplay(CharacterStatModifierData newData)
        {
            icon.sprite = newData.icon;
            text.text = newData.displayText;
            text.fontSize = newData.displayFontSize;
        }
    }
}
