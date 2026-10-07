using System.Collections.Generic;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Services;
using Submodules.Utility.Extensions;
using Submodules.Utility.UI;
using UnityEngine;
using UnityEngine.UI;
using static ToolSmiths.InventorySystem.GUI.Displays.CharacterStatModifierDisplay;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public sealed class CharacterStatModifierDisplay : MonoBehaviour, IDisplay<CharacterStatModifierData>
    {
        public struct CharacterStatModifierData
        {
            private const float MinFontSize = 18f;
            private const float MaxFontSize = 24f;

            private CharacterStatModifier statMod;
            public string displayText;
            public float displayFontSize;
            public Sprite icon;

            public CharacterStatModifierData(CharacterStatModifier characterStatModifier)
                : this(characterStatModifier, (StatComparison?)null) { }

            /// <param name="compareTo">The worn items an equip would displace, compared as a whole - a hovered
            /// two-hander is measured against its weapon and off-hand together. Empty: a free slot, so the row
            /// shows the full effect of equipping it.</param>
            /// <param name="earlier">The hovered item's affixes drawn above this one, so rows that share a stat do not
            /// each take the displaced modifiers off again.</param>
            public CharacterStatModifierData(CharacterStatModifier characterStatModifier, IReadOnlyList<Package> compareTo,
                IReadOnlyList<CharacterStatModifier> earlier = null)
                : this(characterStatModifier, EquipEffect(characterStatModifier, compareTo, earlier)) { }

            /// <summary>A row of worn gear the hovered item displaces: only a stat the hovered item lacks gets a
            /// difference, the cost of the unequip alone.</summary>
            public CharacterStatModifierData(CharacterStatModifier characterStatModifier, ItemInstance displacedBy)
                : this(characterStatModifier, UnequipEffect(characterStatModifier, displacedBy)) { }

            private CharacterStatModifierData(CharacterStatModifier characterStatModifier, StatComparison? comparison)
            {
                statMod = characterStatModifier;

                var comparisonColor = comparison.HasValue ? UiColors.Of(comparison.Value.Verdict) : UiColors.Neutral;

                // Alt trades the comparison for the roll range: the range only while it is held, the difference
                // the rest of the time.
                var altHeld = ModifierKeys.Alt;
                var difference = altHeld ? string.Empty : comparison?.Format() ?? string.Empty;
                var range = altHeld ? $" {statMod.Modifier.Range.ToString().Colored(UiColors.Muted)}" : string.Empty;

                icon = ItemService.Instance.GetStatIcon(statMod.Stat);
                displayText = $"{statMod.Modifier}{range}"
                    + (difference.Length == 0 ? string.Empty : $" {difference.Colored(comparisonColor)}");
                displayFontSize = RollQualityFontSize(statMod.Modifier);
            }

            private static StatComparison? EquipEffect(CharacterStatModifier row, IReadOnlyList<Package> compareTo,
                IReadOnlyList<CharacterStatModifier> earlier)
            {
                var worn = new List<CharacterStatModifier>();

                foreach (var package in compareTo)
                    if (package.IsValid)
                        worn.AddRange(package.Item.Affixes);

                return StatComparison.Of(row, worn, Session.Instance.Hero.CompareStatModifiers, earlier);
            }

            private static StatComparison? UnequipEffect(CharacterStatModifier row, ItemInstance displacedBy) =>
                displacedBy == null ? null : StatComparison.OfLoss(row, displacedBy.Affixes, Session.Instance.Hero.CompareStatModifiers);

            /// <summary>
            /// Font size scales with roll quality: an affix at the bottom of its range renders at
            /// <see cref="MinFontSize"/>, one at the top at <see cref="MaxFontSize"/>. The
            /// <see cref="Mathf.Clamp01"/> keeps a value outside its range — or a degenerate
            /// (zero-width) range — from extrapolating past those bounds and blowing the text up.
            /// </summary>
            private static float RollQualityFontSize(StatModifier modifier)
            {
                var rollQuality = Mathf.Clamp01(modifier.Value.MapTo01(modifier.Range.x, modifier.Range.y));
                return rollQuality.MapFrom01(MinFontSize, MaxFontSize);
            }
        }

        [SerializeField] private Image icon;

        [SerializeField] private TextMeshProUGUI text;

        public void Refresh(CharacterStatModifierData newData)
        {
            icon.sprite = newData.icon;
            text.text = newData.displayText;
            text.fontSize = newData.displayFontSize;
        }
    }
}
