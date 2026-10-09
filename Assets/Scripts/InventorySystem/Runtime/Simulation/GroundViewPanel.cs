using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// A view of the live Run's floor (<see cref="LootFlow.Ground"/>): the Ground Items List and the floor
    /// grid are two of these over the one container. Bound the way <see cref="EnemyArena"/> is, and for the same
    /// reason: the loot flow is rebuilt on every Send and Relocate, and a hero load replaces the Run, so
    /// <see cref="Update"/> compares the current one to the one held, and everything else is the loot flow's
    /// <see cref="LootFlow.GroundChanged"/> event. The event names nothing - <see cref="Items.ItemInstance"/>
    /// is value-equal, so two equal Drops cannot be told apart by value - so every change re-reads the container.
    /// </summary>
    public abstract class GroundViewPanel : MonoBehaviour
    {
        /// <summary>The loot flow this view reads, or <c>null</c> between Runs.</summary>
        protected LootFlow Bound { get; private set; }

        private void Update()
        {
            var lootFlow = SimulationService.Instance.LootFlow;
            if (lootFlow == Bound)
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
            Bound = lootFlow;
            Bound.GroundChanged += Refresh;

            // The Run's first Drops can land before this could listen.
            Refresh();
        }

        // Nothing bound means nothing drawn (the last Unbind emptied the view), so a disabled or torn-down
        // panel that never bound has nothing to release and its Refresh stays unrun.
        private void Unbind()
        {
            if (Bound == null)
                return;

            Bound.GroundChanged -= Refresh;
            Bound = null;
            Refresh();
        }

        /// <summary>Brings the view in step with <see cref="Bound"/>'s ground; with no loot flow bound, empties it.</summary>
        protected abstract void Refresh();
    }
}
