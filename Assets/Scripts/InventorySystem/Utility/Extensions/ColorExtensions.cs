using UnityEngine;

namespace ToolSmiths.InventorySystem.Utility.Extensions
{
    public static class ColorExtensions
    {
        public static Color Orange => new(1f, .5f, 0f, 1f);
        public static Color LightBlue => new(0f, .5f, 1f, 1f);
        public static Color Purple => new(.5f, 0f, 1f, 1f);

        #region rarity colors
        public static Color ItemRarityCommon => Color.white;
        public static Color ItemRarityMagic => new(0, 0.75f, 1, 1); // blue
        public static Color ItemRarityRare => Color.yellow;
        public static Color ItemRarityUnique => new(1, 0.35f, 0, 1); //orange
        #endregion rarity colors
    }
}