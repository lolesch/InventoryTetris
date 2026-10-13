using Submodules.Utility.Services;
using Submodules.Utility.UI;
using TMPro;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Buttons
{
    /// <summary>
    /// The Main Menu's Play: enters the game with the selected hero (<see cref="MainMenuFlow.Play"/>) through the
    /// scene loader and its progress display, and says what that does: "Start Game" for a hero that has no save
    /// yet, "Continue" for one that has. Not interactable once it has been pressed, so a second click cannot
    /// queue a second load; the menu hides it while there is no hero to enter. It follows
    /// <see cref="IHeroSaveService.HeroesChanged"/>, which every select, create and delete raises, and acts in
    /// Play Mode only: <c>Selectable</c> is <c>[ExecuteAlways]</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayButton : AbstractButton
    {
        private IHeroSaveService watched;
        private TMP_Text label;

        protected override void OnEnable()
        {
            base.OnEnable();

            if (!Application.isPlaying || !ServiceLocator.IsArmed)
                return;

            label = GetComponentInChildren<TMP_Text>(true);
            watched = HeroSaveService.Instance;
            watched.HeroesChanged += Refresh;
            Refresh();
        }

        protected override void OnDisable()
        {
            if (watched != null)
            {
                watched.HeroesChanged -= Refresh;
                watched = null;
            }

            base.OnDisable();
        }

        protected override void OnClick()
        {
            // Locks only once the load started: a refused one leaves Play to be pressed again.
            if (MainMenuFlow.Play(HeroSaveService.Instance, LoadScene))
                interactable = false;
        }

        // A missing loader (the application is quitting) counts as a load that did not start.
        private static bool LoadScene(string scene)
        {
            var loader = SceneProvider.Instance;
            return loader && loader.LoadScene(scene);
        }

        private void Refresh()
        {
            // The menu hides this button from its own HeroesChanged handler, and an event runs the handlers it
            // started with: this one can still be called after the disable has let go of the service.
            if (watched == null)
                return;

            interactable = MainMenuFlow.CanPlay(watched);
            label.text = MainMenuFlow.PlayLabel(watched);
        }
    }
}
