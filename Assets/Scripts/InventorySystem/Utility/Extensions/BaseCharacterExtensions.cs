using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;

namespace ToolSmiths.InventorySystem.Utility.Extensions
{
    public static class BaseCharacterExtensions
    {
        public static CharacterStat GetStat(this BaseCharacter character, StatName stat) => character.Hero.GetStat(stat);

        public static CharacterResource GetResource(this BaseCharacter character, StatName resource) => character.Hero.GetResource(resource);

        public static float GetStatValue(this BaseCharacter character, StatName stat) => character.Hero.GetStatValue(stat);
    }
}