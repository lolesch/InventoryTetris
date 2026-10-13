using Submodules.Utility.Services;
using System;
using ToolSmiths.InventorySystem.GUI.Components.Buttons;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Panels
{
    /// <summary>
    /// The game scene's Pause Menu screen: a scrim that blocks clicks and the panel on it, shown while the
    /// <see cref="IPauseMenu"/> is open. It is the <see cref="CancelRank.PauseMenu"/> layer, so a Cancel with
    /// nothing above it opens the menu and a Cancel with it open closes it; Resume does the same by click.
    /// Leaving the scene closes the menu, so the Run is never left held. The component sits on an object
    /// that stays active; <c>prompt</c> is the scrim it shows.
    /// </summary>
    public sealed class PauseMenuPanel : MonoBehaviour
    {
        [SerializeField] private GameObject prompt;
        [SerializeField] private ClickEventButton resume;

        private IPauseMenu menu;
        private IDisposable cancelLayer;

        private void OnEnable()
        {
            resume.Clicked += Close;

            // A scene object survives a Play session, so it starts as the menu is, not as the last session left it.
            if (ServiceLocator.IsArmed && ServiceLocator.Current.TryGet<IPauseMenu>(out menu))
            {
                menu.OpenChanged += Show;
                cancelLayer = CancelChain.TryRegister(CancelRank.PauseMenu, Cancel);
            }

            Show(menu != null && menu.IsOpen);
        }

        private void OnDisable()
        {
            resume.Clicked -= Close;

            cancelLayer?.Dispose();
            cancelLayer = null;

            if (menu != null)
            {
                menu.OpenChanged -= Show;
                menu.Close();
                menu = null;
            }
        }

        private void Show(bool open) => prompt.SetActive(open);

        private void Close() => menu.Close();

        private bool Cancel()
        {
            menu.Toggle();
            return true;
        }
    }
}
