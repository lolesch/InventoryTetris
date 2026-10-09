using System.Collections.Generic;
using Submodules.Utility.Tools;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The Ground Items List (issue #63): one pooled <see cref="GroundItemSlotDisplay"/> per Drop
    /// in the live Run's <see cref="LootFlow.GroundDrops"/>, oldest first, growing and
    /// shrinking as Drops land, are evicted and are picked up. The container's layout group and content size
    /// fitter do the sizing; this only keeps the rows in step with the list. Row <c>i</c> is rebound to Drop
    /// <c>i</c> on every change, which leaves a row whose Drop and amount did not change (and its hover)
    /// untouched. Binding to the Run is <see cref="GroundViewPanel"/>'s.
    ///
    /// A click goes to <see cref="LootFlow.PickUpFromGround"/>, which hands the Drop to the
    /// player's acquisition entry point (<see cref="IItemReceiver"/>): auto-equip, else the bag,
    /// and with no room it stays on the ground and its row stays in the list.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GroundItemsPanel : GroundViewPanel
    {
        [SerializeField] private GroundItemSlotDisplay prefab;
        [SerializeField] private Transform container;

        private readonly List<GroundItemSlotDisplay> _slots = new();
        private PrefabPool<GroundItemSlotDisplay> _pool;

        // Lazily built rather than in Awake, which does not run again on the second Play entry
        // under disabled domain reload - the scene object survives, only OnEnable/Update repeat.
        private PrefabPool<GroundItemSlotDisplay> Pool =>
            _pool ??= new PrefabPool<GroundItemSlotDisplay>(prefab, container != null ? container : transform);

        /// <summary>Rebinds the rows to <see cref="LootFlow.GroundDrops"/>: extras back to the pool, missing ones taken from it.</summary>
        protected override void Refresh()
        {
            var drops = Bound != null ? Bound.GroundDrops : null; // oldest first
            var count = drops?.Count ?? 0;

            while (count < _slots.Count)
            {
                Release(_slots[^1]);
                _slots.RemoveAt(_slots.Count - 1);
            }

            for (var i = 0; i < count; i++)
            {
                if (i == _slots.Count)
                    _slots.Add(Pool.GetObject());

                _slots[i].Bind(drops[i].Item, drops[i].Amount, ItemService.Instance.View(drops[i].Item), OnSlotClicked);
                _slots[i].transform.SetSiblingIndex(i);
            }
        }

        private void OnSlotClicked(ItemInstance item) => _ = Bound?.PickUpFromGround(item);

        // OnDisable also runs as Play mode tears the scene down, when a row may already be
        // destroyed - releasing one would throw, and one exception pauses the Editor.
        private void Release(GroundItemSlotDisplay slot)
        {
            if (slot == null)
                return;

            slot.Unbind();
            Pool.ReleaseObject(slot);
        }
    }
}
