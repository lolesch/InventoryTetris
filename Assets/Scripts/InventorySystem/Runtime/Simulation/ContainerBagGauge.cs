using System;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The <see cref="IBagGauge"/> adapter over a real container (issue #23) — the one thing the
    /// engine-free sim needs to know about storage, so the bag-full auto-Recall can fire.
    ///
    /// Fill is measured in <em>cells</em>, not packages: this is a Tetris grid, so a bag holding
    /// three 2×2 items is half-full at 6×4, and a bag holding twelve 1×1 coins is half-full too.
    /// A stack counts once — it occupies its footprint whatever its Amount. Reads the container
    /// live on every call; the sim asks once a tick and nothing caches the answer.
    /// </summary>
    public sealed class ContainerBagGauge : IBagGauge
    {
        private readonly AbstractDimensionalContainer _container;

        public ContainerBagGauge(AbstractDimensionalContainer container) =>
            _container = container ?? throw new ArgumentNullException(nameof(container));

        /// <summary>
        /// Occupied cells over <see cref="AbstractDimensionalContainer.Capacity"/>, clamped to
        /// <c>[0, 1]</c>. A zero-capacity container reads as full: there is no room in it, which
        /// is what the trigger is asking about.
        /// </summary>
        public float FillFraction
        {
            get
            {
                var capacity = _container.Capacity;
                if (capacity <= 0) return 1f;

                var occupied = 0;
                foreach (var entry in _container.StoredPackages)
                {
                    var footprint = ItemView.Of(entry.Value.Item).Dimensions;
                    occupied += footprint.x * footprint.y;
                }

                var fraction = occupied / (float)capacity;
                return fraction < 0f ? 0f : fraction > 1f ? 1f : fraction;
            }
        }
    }
}
