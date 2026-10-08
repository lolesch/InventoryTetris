using Submodules.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data
{
    /// <summary>How good a rolled modifier is within its range, as the size its text renders at.</summary>
    public static class RollQuality
    {
        /// <summary>
        /// Font size scales with roll quality: an affix at the bottom of its range renders at
        /// <paramref name="minSize"/>, one at the top at <paramref name="maxSize"/>. The clamp keeps a value
        /// outside its range from extrapolating past those bounds. A fixed roll (a zero-width range) cannot
        /// roll any better, so it renders at <paramref name="maxSize"/>.
        /// </summary>
        public static float FontSize(StatModifier modifier, float minSize, float maxSize)
        {
            var range = modifier.Range;
            var quality = range.x == range.y ? 1f : Mathf.Clamp01(modifier.Value.MapTo01(range.x, range.y));
            return quality.MapFrom01(minSize, maxSize);
        }
    }
}
