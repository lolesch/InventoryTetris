using Submodules.Utility.Bindings;
using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// An <see cref="IInputService{TAction}"/> a test presses by hand: no device, no frame. Reach for it
    /// wherever the code under test takes the service; the real one is proven in the Utility submodule.
    /// </summary>
    public sealed class FakeInput : IInputService<GameAction>
    {
        private readonly HashSet<GameAction> held = new();

        public event Action<GameAction> Performed;

        public Vector2 PointerPosition { get; set; }

        public bool IsHeld(GameAction action) => held.Contains(action);

        /// <summary>Holds the action and raises <see cref="Performed"/>, as a key going down does.</summary>
        public void Press(GameAction action)
        {
            _ = held.Add(action);
            Performed?.Invoke(action);
        }

        public void Release(GameAction action) => held.Remove(action);

        public void Rebind(GameAction action, int bindingIndex, Action<RebindResult> completed) => throw new NotSupportedException();

        public void ResetBindings() => throw new NotSupportedException();
    }
}
