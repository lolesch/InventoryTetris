using ToolSmiths.InventorySystem.Locations;
using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The Town Portal on the Town map (ADR-0022), beside the Waypoint: shown exactly while the hero has an open portal,
    /// with a Run held, a Run ended after it, or none at all. The hero owns the portal and no event says it closed
    /// (a resume clears it after the Run's own phase event), so it is read every frame.
    ///
    /// <para>A click uses it, and using it closes it. The hero walks to the portal at his own pace and vanishes into it,
    /// and only then does the Field map come up: with the Run held it resumes it, live exactly where it was held and
    /// with the portal's Location selected, no new fight; with no Run it Sends a fresh Encounter at the portal's
    /// Location, the figure arriving through the portal. The Field map is brought up here, as the Town Exit does,
    /// because the Town map's collapse on the Run going live is refused (it is the group's active member).</para>
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class TownPortalMarker : AbstractButton
    {
        [SerializeField, Tooltip("The Field map's face, which the portal brings up.")]
        private FieldFacePanel fieldFace;
        [SerializeField, Tooltip("The Field map's Hero figure, which arrives through the portal when a Run is Sent from it.")]
        private HeroFigure fieldFigure;
        [SerializeField, Tooltip("The Town map's Hero figure, which walks into the portal before the map changes.")]
        private TownHeroFigure townFigure;

        private Image _image;
        private bool _using;

        private Image Image => _image ? _image : _image = GetComponent<Image>();

        private void Update()
        {
            var open = Session.Instance.Hero.TownPortal != null;
            if (!open)
                _using = false;

            Image.enabled = open;
            interactable = open && !_using;
        }

        protected override void OnClick()
        {
            var simulation = SimulationService.Instance;
            var location = Session.Instance.Hero.TownPortal;

            // A live Run has left the Town map: a click in the last of its fade does not use the portal.
            if (location == null || simulation.Run.Phase == RunPhase.Live || !simulation.CanSend(location))
                return;

            _using = true;
            townFigure.WalkIntoPortal((RectTransform)transform, () => Use(location));
        }

        private void Use(LocationConfig location)
        {
            var simulation = SimulationService.Instance;

            _using = false;
            if (!simulation.CanSend(location))
                return;

            fieldFace.Expand();
            InventoryService.Instance.SyncContextToMap(true);

            if (simulation.Run.Phase == RunPhase.Held)
                simulation.Resume();
            else
            {
                simulation.Send(location);
                fieldFigure.ArriveByPortal();
                Session.Instance.Hero.TownPortal = null;
            }

            // After the Run: the toggle follows it, so its OnToggle finds the Run already live here and commands nothing.
            foreach (var toggle in FindObjectsByType<LocationToggle>(FindObjectsInactive.Include))
                if (toggle.Location == location)
                    toggle.SyncToggle(true);
        }
    }
}
