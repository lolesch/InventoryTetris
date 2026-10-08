using System;
using System.Collections.Generic;

namespace ToolSmiths.InventorySystem.Geometry
{
    /// <summary>
    /// Sums the damage pushed per key so a frame shows one number each. A coarse frame runs up to
    /// <c>MaxTicksPerAdvance</c> ticks at once and every tick can land a hit, so showing one
    /// number per hit would flood the screen. <see cref="Flush"/> hands each total out once and resets.
    /// </summary>
    /// <typeparam name="TKey">What the damage is attributed to; the arena keys it by target and damage type.</typeparam>
    public sealed class DamageAccumulator<TKey>
    {
        private readonly Dictionary<TKey, (float Amount, float Raw, int Hits)> totals = new();
        private readonly List<KeyValuePair<TKey, (float Amount, float Raw, int Hits)>> pending = new();

        /// <summary>
        /// Adds a hit to <paramref name="key"/>'s total for this frame: <paramref name="amount"/> is what the
        /// target lost, the figure a number shows, and <paramref name="raw"/> is the hit before the target's
        /// mitigation, the figure its size is read from. Both are summed, and the hits are counted so the size
        /// can be read from one hit (raw / hits) rather than from the whole frame's sum.
        /// </summary>
        public void Add(TKey key, float amount, float raw)
        {
            totals.TryGetValue(key, out var total);
            totals[key] = (total.Amount + amount, total.Raw + raw, total.Hits + 1);
        }

        /// <summary>
        /// Calls <paramref name="sink"/> once per key that took damage since the last flush, with the summed
        /// amount, the summed raw amount and the number of hits behind them, then clears.
        /// </summary>
        public void Flush(Action<TKey, float, float, int> sink)
        {
            // cleared before the sink runs, so a sink that adds again starts the next frame
            pending.Clear();
            pending.AddRange(totals);
            totals.Clear();

            foreach (var entry in pending)
                sink(entry.Key, entry.Value.Amount, entry.Value.Raw, entry.Value.Hits);
        }
    }
}
