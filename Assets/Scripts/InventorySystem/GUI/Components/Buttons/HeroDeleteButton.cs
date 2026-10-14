using Submodules.Utility.Services;
using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.GUI.Components.Popups;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Buttons
{
    /// <summary>
    /// The Main Menu's Delete: asks the <see cref="HeroDeletePopup"/> to confirm deleting the selected hero,
    /// readable or not. Not interactable with no hero selected. It follows
    /// <see cref="IHeroSaveService.HeroesChanged"/>, which every select, create and delete raises, and acts in
    /// Play Mode only: <c>Selectable</c> is <c>[ExecuteAlways]</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroDeleteButton : AbstractButton
    {
        [SerializeField] private HeroDeletePopup popup;

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

        protected override void OnClick() => popup.Open();

        // The menu hides this button from its own HeroesChanged handler, and an event runs the handlers it
        // started with: this one can still be called after the disable has let go of the service.
        private void Refresh()
        {
            if (watched != null)
                interactable = MainMenuFlow.TrySelected(watched, out _);
        }
    }
}
