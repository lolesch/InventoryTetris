using Submodules.Utility.Services;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The Corpse variant of the Hero icon: shown under its <see cref="LocationToggle"/> while the Hero's
    /// Corpse lies at that Location, whether or not the Location is selected. It is a sibling of the Hero
    /// icon's <c>ToggleCheckmark</c>, never that image, so the arena's anchor and the normal Hero icon are
    /// untouched. It repaints on <see cref="ISimulationService.CorpseChanged"/>.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class CorpseMarker : MonoBehaviour
    {
        private Image image;
        private LocationToggle toggle;

        private void Awake()
        {
            image = GetComponent<Image>();
            toggle = GetComponentInParent<LocationToggle>(true);

            // Without a Location to ask there is nothing to show, so the image would stay as serialized.
            if (toggle == null)
                image.enabled = false;
        }

        // The service is built after Awake, so the first look is here rather than in OnEnable.
        private void Start()
        {
            if (toggle == null)
                return;

            SimulationService.Instance.CorpseChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            // The service is gone while Play Mode tears the scene down.
            if (toggle != null && ServiceLocator.IsArmed)
                SimulationService.Instance.CorpseChanged -= Refresh;
        }

        private void Refresh() =>
            image.enabled = SimulationService.Instance.CorpseLiesAt(toggle.Location);
    }
}
