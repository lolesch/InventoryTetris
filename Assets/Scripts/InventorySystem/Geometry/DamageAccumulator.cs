using System;
using System.Collections.Generic;

namespace ToolSmiths.InventorySystem.Geometry
{
    /// <summary>
    /// Sums the damage pushed per enemy so a frame shows one number each. A coarse frame runs up to
    /// <c>MaxTicksPerAdvance</c> ticks at once and every tick raises the health event, so showing one
    /// number per event would flood the screen. <see cref="Flush"/> hands each total out once and resets.
    /// </summary>
    /// <typeparam name="TKey">What the damage is attributed to; the enemy, in the arena.</typeparam>
    public sealed class DamageAccumulator<TKey>
    {
        private readonly Dictionary<TKey, float> totals = new();
        private readonly List<KeyValuePair<TKey, float>> pending = new();

        /// <summary>Adds <paramref name="amount"/> to <paramref name="key"/>'s total for this frame.</summary>
        public void Add(TKey key, float amount) =>
            totals[key] = totals.TryGetValue(key, out var total) ? total + amount : amount;

        /// <summary>Calls <paramref name="sink"/> once per key that took damage since the last flush, then clears.</summary>
        public void Flush(Action<TKey, float> sink)
        {
            /// cleared before the sink runs, so a sink that adds again starts the next frame
            pending.Clear();
            pending.AddRange(totals);
            totals.Clear();

            foreach (var entry in pending)
                sink(entry.Key, entry.Value);
        }
    }
}
