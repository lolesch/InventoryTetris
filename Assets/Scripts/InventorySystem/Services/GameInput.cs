using Submodules.Utility.Bindings;
using Submodules.Utility.Services;
using System;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The held keys and the pointer, for a view that reads them every frame. Asked of the armed
    /// <see cref="IInputService{TAction}"/> and answering "nothing held, pointer at the origin" when
    /// there is none (an EditMode test that arms its own registry, a scene object enabled outside
    /// Play), so a view never guards its own read.
    ///
    /// <para>A press is <see cref="Performed"/>: one event that outlives any one boot's service (the
    /// boot forwards each service's presses into it), so a view subscribes in <c>OnEnable</c>
    /// whether or not a boot has run yet, and a later boot cannot leave it listening to a dead service.</para>
    ///
    /// <para>This is the lookup, not a second contract: the interface is
    /// <see cref="IInputService{TAction}"/>, which a class that is handed its input takes by constructor
    /// (<see cref="PauseHotkey"/>) and a scene view reaches through here, as it reaches every other
    /// service through the locator. A test fakes it by registering a <c>FakeInput</c> in a registry.</para>
    /// </summary>
    public static class GameInput
    {
        /// <summary>A button action went down, from whichever service is armed.</summary>
        public static event Action<GameAction> Performed;

        internal static void Raise(GameAction action) => Performed?.Invoke(action);

        public static bool IsHeld(GameAction action) => Armed?.IsHeld(action) ?? false;

        public static Vector2 PointerPosition => Armed?.PointerPosition ?? Vector2.zero;

        /// <summary>The armed service, or null.</summary>
        public static IInputService<GameAction> Armed =>
            ServiceLocator.IsArmed && ServiceLocator.Current.TryGet<IInputService<GameAction>>(out var service) ? service : null;
    }
}
