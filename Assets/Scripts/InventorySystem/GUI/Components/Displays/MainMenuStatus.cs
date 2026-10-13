using Submodules.Utility.Services;
using TMPro;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    /// <summary>
    /// The Main Menu's platform and hero rules. In a browser <see cref="exitButton"/> is hidden, since quitting
    /// does nothing there, and <see cref="savedLine"/> tells the player when their last-selected hero was saved,
    /// following <see cref="IHeroSaveService.HeroesChanged"/>. Native builds keep Exit and hide the line. Play
    /// and Delete are shown only with a hero to act on: a menu with none offers the hero slots alone.
    /// Acts in Play Mode only, so an enable in the Editor does not rewrite the authored buttons.
    /// </summary>
    public sealed class MainMenuStatus : MonoBehaviour
    {
        [Tooltip("The Exit button: hidden in WebGL.")]
        [SerializeField] private GameObject exitButton;

        [Tooltip("The Play button: shown while a hero that reads is selected.")]
        [SerializeField] private GameObject playButton;

        [Tooltip("The Delete button: shown while any hero, readable or not, is selected.")]
        [SerializeField] private GameObject deleteButton;

        [Tooltip("The line that names the last-selected hero and when it was saved: shown in WebGL only.")]
        [SerializeField] private TextMeshProUGUI savedLine;

        private IHeroSaveService watched;

        private void OnEnable()
        {
            if (!Application.isPlaying || !ServiceLocator.IsArmed)
                return;

            watched = HeroSaveService.Instance;
            watched.HeroesChanged += Refresh;
            Apply(Application.platform);
        }

        private void OnDisable()
        {
            if (watched == null)
                return;

            watched.HeroesChanged -= Refresh;
            watched = null;
        }

        /// <summary>Lays the menu out for <paramref name="platform"/>; the enable passes the real one.</summary>
        public void Apply(RuntimePlatform platform)
        {
            var exits = MainMenuFlow.ShowsExit(platform);

            exitButton.SetActive(exits);
            savedLine.gameObject.SetActive(!exits);
            Refresh();
        }

        private void Refresh()
        {
            savedLine.text = MainMenuFlow.SavedLine(watched) ?? string.Empty;
            playButton.SetActive(MainMenuFlow.CanPlay(watched));
            deleteButton.SetActive(MainMenuFlow.TrySelected(watched, out _));
        }
    }
}
