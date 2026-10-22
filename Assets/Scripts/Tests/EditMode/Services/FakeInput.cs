using Submodules.Utility.Bindings;
using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// An <see cref="IInputService{TAction}"/> a test presses by hand: no device, no frame. Reach for it
    /// wherever the code under test takes the service; the real one is proven in the Utility submodule.
    /// </summary>
    public sealed class FakeInput : IInputService<GameAction>
    {
        private readonly HashSet<GameAction> held = new();
        private readonly HashSet<Key> heldKeys = new();

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

        public bool IsKeyHeld(Key key) => heldKeys.Contains(key);

        /// <summary>Holds the physical key, whatever it is bound to. Does not press an action: pair it with
        /// <see cref="Press"/> for a key that is bound to one.</summary>
        public void PressKey(Key key) => heldKeys.Add(key);

        public void ReleaseKey(Key key) => heldKeys.Remove(key);

        public void Rebind(GameAction action, int bindingIndex, Action<RebindResult> completed) => throw new NotSupportedException();

        public void ResetBindings() => throw new NotSupportedException();
    }
}
