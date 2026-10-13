using Submodules.Utility.Extensions;

namespace ToolSmiths.InventorySystem.Data
{
    /// <summary>How good a rolled modifier is within its range, as the size its text renders at.</summary>
    public static class RollQuality
    {
        /// <summary>
        /// Font size scales with roll quality: an affix at the bottom of its range renders at
        /// <paramref name="minSize"/>, one at the top at <paramref name="maxSize"/>. <c>Map</c> clamps, so a value
        /// outside its range cannot leave those bounds. A fixed roll (a zero-width range) cannot
        /// roll any better, so it renders at <paramref name="maxSize"/>.
        /// </summary>
        public static float FontSize(StatModifier modifier, float minSize, float maxSize)
        {
            var range = modifier.Range;
            return range.x == range.y ? maxSize : modifier.Value.Map(range.x, range.y, minSize, maxSize);
        }
    }
}
