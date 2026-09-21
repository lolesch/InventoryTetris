using System;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// An inclusive <c>[Min, Max]</c> integer range, rolled fresh from an <see cref="IRollSource"/>.
    /// Stands in for the <c>Vector2Int</c> the <c>LocationConfig</c> ScriptableObject (issue #25)
    /// will author — kept engine-free here so the Encounter sim stays Unity-free.
    /// </summary>
    public readonly struct IntRange
    {
        public readonly int Min;
        public readonly int Max;

        public IntRange(int min, int max)
        {
            Min = min;
            Max = max;
        }

        public IntRange(int fixedValue) : this(fixedValue, fixedValue) { }

        /// <summary>
        /// A value in <c>[Min, Max]</c>. Matches the <c>/prototype</c>'s
        /// <c>round(lo + r·(hi − lo))</c> — the endpoints carry half weight, which is moot for
        /// the MVP's fixed <c>[n, n]</c> rosters but keeps the tuning surface identical.
        /// </summary>
        public int Roll(IRollSource rolls)
        {
            if (Max <= Min) return Min;
            return Min + (int)Math.Round(rolls.Next() * (Max - Min), MidpointRounding.AwayFromZero);
        }
    }
}
