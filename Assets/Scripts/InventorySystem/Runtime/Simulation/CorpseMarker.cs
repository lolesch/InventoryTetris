using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The Corpse variant of the Hero icon: shown under its <see cref="LocationToggle"/> while the Hero's
    /// Corpse lies at that Location, whether or not the Location is selected. It is a sibling of the Hero
    /// icon's <c>ToggleCheckmark</c>, never that image, so the arena's anchor and the normal Hero icon are
    /// untouched. The Corpse raises no event, so it polls; a recovery or a Death elsewhere clears it
    /// within a frame.
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
            enabled = toggle != null;
        }

        private void Update() =>
            image.enabled = SimulationService.Instance.CorpseLiesAt(toggle.Location);
    }
}
