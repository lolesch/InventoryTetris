using System;
using Submodules.Utility.UI;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The Combat panel's <c>AutoPickup</c> debug switch (issue #63): on, the loot flow picks up
    /// the Drops the filter admits as it did before the Ground Items List; off, every item lies on
    /// the ground for a click. It only reports its state - <see cref="BehaviourSlidersPanel"/>
    /// writes it to the <see cref="ToolSmiths.InventorySystem.Simulation.HeroBehaviour"/> the same
    /// way it does for the sliders.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AutoPickupToggle : AbstractToggle
    {
        /// <summary>Raised with the new state each time the toggle switches, by click or by <see cref="AbstractToggle.SyncToggle"/>.</summary>
        public event Action<bool> Toggled;

        protected override void OnToggle() => Toggled?.Invoke(IsOn);
    }
}
