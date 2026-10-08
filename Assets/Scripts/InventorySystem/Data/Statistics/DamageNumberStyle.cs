using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data
{
    /// <summary>
    /// How one damage number is drawn (issue #213): its font size and tint. The size shows how big the hit was
    /// against the best the dealer could do with that damage type; the tint tells the type apart.
    /// </summary>
    public readonly struct DamageNumberStyle
    {
        public DamageNumberStyle(float fontSize, Color tint)
        {
            FontSize = fontSize;
            Tint = tint;
        }

        public float FontSize { get; }

        public Color Tint { get; }

        /// <summary>
        /// The style of a hit of <paramref name="amount"/>: the clamped map of the amount from zero up to
        /// <paramref name="referenceMax"/> onto <paramref name="minSize"/>..<paramref name="maxSize"/>, tinted by
        /// <paramref name="type"/>. With no reference above zero nothing could hit harder, so the hit is the best
        /// there is and renders at <paramref name="maxSize"/> - the fixed-roll case of <see cref="RollQuality.FontSize"/>.
        /// </summary>
        public static DamageNumberStyle Of(float amount, float referenceMax, DamageType type, float minSize, float maxSize,
            Color physicalTint, Color magicalTint)
        {
            var size = referenceMax > 0f ? amount.MapClamped(0f, referenceMax, minSize, maxSize) : maxSize;
            return new DamageNumberStyle(size, type == DamageType.MagicalDamage ? magicalTint : physicalTint);
        }
    }
}
