using Submodules.Utility.Bindings;
using Submodules.Utility.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The held keys and the pointer, for a view that reads them every frame. Asked of the armed
    /// <see cref="IInputService{TAction}"/> and answering "nothing held, pointer at the origin" when
    /// there is none (an EditMode test that arms its own registry, a scene object enabled outside
    /// Play), so a view never guards its own read. A press is not read here: a view subscribes to
    /// <see cref="Armed"/>'s <c>Performed</c>.
    /// </summary>
    public static class GameInput
    {
        public static bool IsHeld(GameAction action) => Armed?.IsHeld(action) ?? false;

        public static Vector2 PointerPosition => Armed?.PointerPosition ?? Vector2.zero;

        /// <summary>The armed service, or null. What a view subscribes to in <c>OnEnable</c>, and lets
        /// go of in <c>OnDisable</c> from the instance it subscribed to.</summary>
        public static IInputService<GameAction> Armed =>
            ServiceLocator.IsArmed && ServiceLocator.Current.TryGet<IInputService<GameAction>>(out var service) ? service : null;
    }
}
