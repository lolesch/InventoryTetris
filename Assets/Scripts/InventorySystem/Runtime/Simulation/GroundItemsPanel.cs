using System.Collections.Generic;
using Submodules.Utility.Tools;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.GUI.InventoryDisplays;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The Ground Items List (issue #63): one pooled <see cref="GroundItemSlotDisplay"/> per Drop
    /// on the ground, oldest first, growing and shrinking as Drops land, are evicted and are picked up. It
    /// binds the ground by its role like any container display, and is the second view of that container beside the
    /// ground grid. The rows' layout group and content size fitter do the sizing; this only keeps the rows in
    /// step with the container, and fades each row by its Drop's age like the grid's slots do. Row <c>i</c> is rebound to Drop <c>i</c> on every change, which leaves a row whose
    /// Drop and amount did not change (and its hover) untouched.
    ///
    /// A click goes to <see cref="LootFlow.PickUpFromGround"/>, which hands the Drop to the
    /// player's acquisition entry point (<see cref="IItemReceiver"/>): auto-equip, else the bag,
    /// and with no room it stays on the ground and its row stays in the list.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class GroundItemsPanel : AbstractContainerDisplay
    {
        [SerializeField] private GroundItemSlotDisplay prefab;
        [SerializeField] private Transform rows;
        [SerializeField, Tooltip("The same fade as the ground grid's slots.")] private GroundFadeSettings fade = new();

        private readonly List<GroundItemSlotDisplay> _slots = new();
        private PrefabPool<GroundItemSlotDisplay> _pool;

        // Lazily built rather than in Awake, which does not run again on the second Play entry
        // under disabled domain reload - the scene object survives, only OnEnable repeats.
        private PrefabPool<GroundItemSlotDisplay> Pool =>
            _pool ??= new PrefabPool<GroundItemSlotDisplay>(prefab, rows != null ? rows : transform);

        // The rows follow the Drops, not the cells.
        protected override void SetupSlotDisplays() { }

        /// <summary>Rebinds the rows to the ground's Drops, oldest first: extras back to the pool, missing ones taken from it.</summary>
        protected override void Refresh(Dictionary<Vector2Int, Package> storedPackages)
        {
            var drops = (Container as GroundContainer)?.PackagesOldestFirst();
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
                _slots[i].SetAlpha(fade.AlphaOf(count - 1 - i));
                _slots[i].transform.SetSiblingIndex(i);
            }
        }

        private static void OnSlotClicked(ItemInstance item) => _ = SimulationService.Instance.LootFlow?.PickUpFromGround(item);

        // Play mode tearing the scene down may already have destroyed a row - releasing one would
        // throw, and one exception pauses the Editor.
        private void Release(GroundItemSlotDisplay slot)
        {
            if (slot == null)
                return;

            slot.Unbind();
            Pool.ReleaseObject(slot);
        }
    }
}
