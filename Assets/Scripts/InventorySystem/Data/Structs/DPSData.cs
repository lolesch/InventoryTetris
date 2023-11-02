using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data
{
    public struct DPSData
    {
        private BaseCharacter character;
        public string displayText;
        public Sprite icon;

        public DPSData(BaseCharacter character, DamageType damageType)
        {
            this.character = character;
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