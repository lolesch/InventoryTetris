using System;
using Submodules.Utility.UI;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// This toggle invokes an event Action<bool> to listen to.
    /// Raised with the new state each time the toggle switches, by click or by <see cref="AbstractToggle.SyncToggle"/>.
    /// </summary>
    public sealed class EventToggle : AbstractToggle
    {
        public event Action<bool> Toggled;

        protected override void OnToggle() => Toggled?.Invoke(IsOn);
    }
}
