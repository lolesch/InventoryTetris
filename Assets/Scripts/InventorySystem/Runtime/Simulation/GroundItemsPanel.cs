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
    /// in the live Run's <see cref="LootFlow.GroundDrops"/>, in ground order, growing and
    /// shrinking as Drops land and are picked up. The container's layout group and content size
    /// fitter do the sizing; this only keeps the rows in step with the list.
    ///
    /// Bound the way <see cref="EnemyArena"/> is, and for the same reason: the loot flow is
    /// rebuilt on every Send and Relocate, and a hero load replaces the Run, so <see cref="Update"/>
    /// compares the current one to the one it holds, and everything else is the loot flow's
    /// <see cref="LootFlow.GroundChanged"/> event. The event names nothing - <see cref="ItemInstance"/>
    /// is value-equal, so two equal Drops cannot be told apart by value - so every change re-reads
    /// the list and rebinds row <c>i</c> to Drop <c>i</c>, which leaves a row whose Drop did not
    /// move (and its hover) untouched.
    ///
    /// A click goes to <see cref="LootFlow.PickUpFromGround"/>, which hands the Drop to the
    /// player's acquisition entry point (<see cref="IItemReceiver"/>): auto-equip, else the bag,
    /// and with no room it stays on the ground and its row stays in the list.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GroundItemsPanel : MonoBehaviour
    {
        [SerializeField] private GroundItemSlotDisplay prefab;
        [SerializeField] private Transform container;

        private readonly List<GroundItemSlotDisplay> _slots = new();
        private PrefabPool<GroundItemSlotDisplay> _pool;
        private LootFlow _bound;

        // Lazily built rather than in Awake, which does not run again on the second Play entry
        // under disabled domain reload - the scene object survives, only OnEnable/Update repeat.
        private PrefabPool<GroundItemSlotDisplay> Pool =>
            _pool ??= new PrefabPool<GroundItemSlotDisplay>(prefab, container != null ? container : transform);

        private void Update()
        {
            var lootFlow = SimulationService.Instance.LootFlow;
            if (lootFlow == _bound)
                return;

            Unbind();
            if (lootFlow != null)
                Bind(lootFlow);
        }

        // Update never runs while disabled, so a disabled panel would hold a subscription no one
        // can retire. Letting go here means the first Update after re-enabling binds afresh.
        private void OnDisable() => Unbind();

        private void Bind(LootFlow lootFlow)
        {
            _bound = lootFlow;
            _bound.GroundChanged += Refresh;

            // The Run's first Drops can land, or a Corpse be laid out, before this could listen.
            Refresh();
        }

        private void Unbind()
        {
            if (_bound != null)
            {
                _bound.GroundChanged -= Refresh;
                _bound = null;
            }

            Refresh();
        }

        /// <summary>Rebinds the rows to <see cref="LootFlow.GroundDrops"/>: extras back to the pool, missing ones taken from it.</summary>
        private void Refresh()
        {
            var drops = _bound != null ? _bound.GroundDrops : null;
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

                _slots[i].Bind(drops[i], OnSlotClicked);
                _slots[i].transform.SetSiblingIndex(i);
            }
        }

        private void OnSlotClicked(ItemInstance item) => _ = _bound?.PickUpFromGround(item);

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
