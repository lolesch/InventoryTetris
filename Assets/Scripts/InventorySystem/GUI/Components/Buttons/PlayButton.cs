using Submodules.Utility.Services;
using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Buttons
{
    /// <summary>
    /// The Main Menu's Play: enters the game with the selected hero (<see cref="MainMenuFlow.Play"/>) through the
    /// scene loader and its progress display. Not interactable until a hero is selected, and again once it has
    /// been pressed, so a second click cannot queue a second load. It follows
    /// <see cref="IHeroSaveService.HeroesChanged"/>, which every select, create and delete raises, and acts in
    /// Play Mode only: <c>Selectable</c> is <c>[ExecuteAlways]</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayButton : AbstractButton
    {
        private IHeroSaveService watched;

        protected override void OnEnable()
        {
            base.OnEnable();

            if (!Application.isPlaying || !ServiceLocator.IsArmed)
                return;

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
            if (MainMenuFlow.Play(HeroSaveService.Instance, scene => SceneProvider.Instance.LoadScene(scene)))
                interactable = false;
        }

        private void Refresh() => interactable = MainMenuFlow.CanPlay(watched);
    }
}
