using Submodules.Utility.Services;
using ToolSmiths.InventorySystem.Geometry;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The Corpse variant of the Hero icon: shown under its <see cref="LocationToggle"/> while the Hero's
    /// Corpse lies at that Location, whether or not the Location is selected. It stands where the hero fell,
    /// projected by the <see cref="EnemyArena"/>'s scale and tilt around the toggle's centre, which is the
    /// arena's anchor, so it sits where the hero's figure stands on that Location. It is a sibling of the Hero icon's <c>ToggleCheckmark</c>, never that image, so the
    /// arena's anchor and the normal Hero icon are untouched. It repaints on
    /// <see cref="ISimulationService.CorpseChanged"/>, and after a Death it waits for the Death beat to end
    /// (<see cref="ISimulationService.DeathBeatEnded"/>), since the hero's figure is lying where it will appear.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class CorpseMarker : MonoBehaviour
    {
        [SerializeField, Tooltip("Supplies the scale and tilt the hero's figure is drawn with.")]
        private EnemyArena arena;

        private Image image;
        private LocationToggle toggle;

        private void Awake()
        {
            image = GetComponent<Image>();
            toggle = GetComponentInParent<LocationToggle>(true);

            // Without a Location to ask or an arena to project by there is nothing to show, so the image
            // would stay as serialized.
            if (toggle == null || arena == null)
                image.enabled = false;
        }

        // The service is built after Awake, so the first look is here rather than in OnEnable.
        private void Start()
        {
            if (toggle == null || arena == null)
                return;

            SimulationService.Instance.CorpseChanged += Refresh;
            SimulationService.Instance.DeathBeatEnded += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            // The service is gone while Play Mode tears the scene down.
            if (toggle != null && arena != null && ServiceLocator.IsArmed)
            {
                SimulationService.Instance.CorpseChanged -= Refresh;
                SimulationService.Instance.DeathBeatEnded -= Refresh;
            }
        }

        private void Refresh()
        {
            // The Corpse is laid the moment the hero goes down, but the figure lying there is what shows until the Death beat ends.
            var simulation = SimulationService.Instance;
            image.enabled = !simulation.DeathBeatPlaying && simulation.CorpseLiesAt(toggle.Location);
            if (!image.enabled)
                return;

            // The toggle's centre is the arena's anchor, and the marker's own parent (the Hero icon) is not
            // centred on it, so the offset is applied in the toggle's space and the result placed by world position.
            var fell = Session.Instance.Hero.Corpse.FallPosition;
            var offset = ArenaLayout.CorpseOffset(fell, simulation.ArenaOrigin, arena.Projection);
            var anchor = (RectTransform)toggle.transform;
            transform.position = anchor.TransformPoint(anchor.rect.center) + anchor.TransformVector(offset);
        }
    }
}
