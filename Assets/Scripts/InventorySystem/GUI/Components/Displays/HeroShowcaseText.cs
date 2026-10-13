using Submodules.Utility.Services;
using TMPro;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    /// <summary>
    /// The Main Menu's text area beside the selected hero's gear: its Location, Corpse, Kills and average
    /// gear level (<see cref="HeroShowcase"/>). Reads the Session's Hero and repaints when a hero loads or the
    /// selection changes; with no hero selected it is blank, since the Session then holds a hero nobody chose.
    /// Subscribes in <see cref="OnEnable"/>, not <c>Awake</c>, which a scene object keeps across Play entries
    /// without its subscriptions (see <c>codebase-notes.md</c>).
    /// </summary>
    public sealed class HeroShowcaseText : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label;

        private IHeroSaveService watched;

        private void OnEnable()
        {
            if (!Application.isPlaying || !Session.TrySubscribeHeroLoaded(Refresh))
                return;

            watched = HeroSaveService.Instance;
            watched.HeroesChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Session.UnsubscribeHeroLoaded(Refresh);

            if (watched == null)
                return;

            watched.HeroesChanged -= Refresh;
            watched = null;
        }

        private void Refresh() => label.text = MainMenuFlow.CanPlay(watched)
            ? HeroShowcase.Text(Session.Instance.Hero, SimulationService.Instance.Locations, ItemService.Instance.Catalog)
            : string.Empty;
    }
}
