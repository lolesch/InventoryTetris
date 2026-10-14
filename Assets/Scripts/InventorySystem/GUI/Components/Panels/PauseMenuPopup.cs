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
    /// that stays active; <c>popup</c> is the scrim it shows. Save &amp; Exit and Quit Game hand over to
    /// <see cref="SaveExitPopup"/> and stop taking clicks once the game is leaving, so a second press cannot
    /// queue a second scene load. Quit Game is hidden where <see cref="GameExit.CanQuit"/> says quitting does nothing.
    /// </summary>
    public sealed class PauseMenuPopup : MonoBehaviour
    {
        [SerializeField] private GameObject popup;
        [SerializeField] private ClickEventButton resume;
        [SerializeField] private ClickEventButton saveExit;
        [SerializeField] private ClickEventButton quitGame;
        [SerializeField] private SaveExitPopup saveExitPopup;

        private IPauseMenu menu;
        private IDisposable cancelLayer;

        private void OnEnable()
        {
            resume.Clicked += Close;
            saveExit.Clicked += saveExitPopup.BeginSaveAndExit;
            quitGame.Clicked += saveExitPopup.BeginQuit;
            saveExitPopup.Left += Leaving;
            saveExit.interactable = true;
            quitGame.interactable = true;
            Apply(Application.platform);

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
            saveExit.Clicked -= saveExitPopup.BeginSaveAndExit;
            quitGame.Clicked -= saveExitPopup.BeginQuit;
            saveExitPopup.Left -= Leaving;

            cancelLayer?.Dispose();
            cancelLayer = null;

            if (menu != null)
            {
                menu.OpenChanged -= Show;
                menu.Close();
                menu = null;
            }
        }

        /// <summary>Lays the menu out for <paramref name="platform"/>; the enable passes the real one.</summary>
        public void Apply(RuntimePlatform platform) => quitGame.gameObject.SetActive(GameExit.CanQuit(platform));

        private void Show(bool open) => popup.SetActive(open);

        private void Close() => menu.Close();

        private void Leaving()
        {
            saveExit.interactable = false;
            quitGame.interactable = false;
        }

        private bool Cancel()
        {
            menu.Toggle();
            return true;
        }
    }
}
