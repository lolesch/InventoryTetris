using Submodules.Utility.Services;
using Submodules.Utility.UI;
using System;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// What a Popup does with its buttons so Enter does the safe thing: it opens with one button selected
    /// (the yes option, or the no option when yes would delete or discard something), and the UI Submit key
    /// then presses it. A Notice (one button, nothing to choose) also routes Cancel to that button.
    /// </summary>
    public static class PopupFocus
    {
        /// <summary>
        /// Selects <paramref name="button"/> as the popup's default. Does nothing, and says so, when it cannot
        /// be pressed or there is no event system to hold a selection.
        /// </summary>
        /// <returns>Whether the button is now the selection.</returns>
        public static bool SelectDefault(Selectable button)
        {
            if (!button || !button.isActiveAndEnabled || !button.interactable || !EventSystem.current)
                return false;

            EventSystem.current.SetSelectedGameObject(button.gameObject);
            return true;
        }

        /// <summary>
        /// Opens a Notice on <paramref name="button"/>: selects it, and registers a <see cref="CancelRank.Popup"/>
        /// layer that presses it, so Esc and Enter both dismiss it. The layer consumes the press even when the
        /// button cannot be pressed, so the Pause Menu behind stays shut. Dispose the result when the Notice closes.
        /// </summary>
        /// <returns>The layer, or <c>null</c> when the services are not armed (an enable in Edit Mode).</returns>
        public static IDisposable OpenNotice(AbstractButton button) =>
            OpenNotice(button, ServiceLocator.IsArmed && ServiceLocator.Current.TryGet<ICancelChain>(out var chain) ? chain : null);

        internal static IDisposable OpenNotice(AbstractButton button, ICancelChain chain)
        {
            _ = SelectDefault(button);

            return chain?.Register(CancelRank.Popup, () =>
            {
                // The same path as Enter: the interactable check, then the click.
                button.OnSubmit(null);
                return true;
            });
        }
    }
}
