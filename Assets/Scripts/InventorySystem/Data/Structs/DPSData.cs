using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Runtime.Provider;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data
{
    public struct DPSData
    {
        public string displayText;
        public Sprite icon;

        public DPSData(BaseCharacter character, DamageType damageType)
        {
            displayText = $"DPS: {character.CalculateDamageOutput(damageType)}";

            var damageTypeToStatIcon = damageType switch
            {
                DamageType.PhysicalDamage => StatName.PhysicalDamage,
                DamageType.MagicalDamage => StatName.MagicalDamage,

                _ => throw new System.NotImplementedException(),
            };

            icon = ItemProvider.Instance.ItemTypeData.GetStatIcon(damageTypeToStatIcon);
        }
    }
}