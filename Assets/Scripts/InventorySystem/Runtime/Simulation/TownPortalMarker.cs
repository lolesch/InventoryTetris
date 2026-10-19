using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The Town Portal on the Town map (ADR-0022), beside the Waypoint: shown exactly while the hero has an open portal,
    /// with a Run held, a Run ended after it, or none at all. The hero owns the portal and no event says it closed
    /// (a resume clears it after the Run's own phase event), so it is read every frame.
    ///
    /// <para>A click uses it. With the Run held it resumes it: the Field map comes back, the Run is live exactly where it
    /// was held and the portal's Location is selected without a new fight. With no Run it Sends a fresh Encounter at the
    /// portal's Location, the figure arriving through the portal. The Field map is brought up here, as the Town Exit
    /// does, because the Town map's collapse on the Run going live is refused (it is the group's active member).</para>
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class TownPortalMarker : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField, Tooltip("The Field map's face, which the portal brings up.")]
        private FieldFacePanel fieldFace;
        [SerializeField, Tooltip("The Field map's Hero figure, which arrives through the portal when a Run is Sent from it.")]
        private HeroFigure fieldFigure;

        private Image _image;

        private Image Image => _image ? _image : _image = GetComponent<Image>();

        private void Update() => Image.enabled = Session.Instance.Hero.TownPortal != null;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            var location = Session.Instance.Hero.TownPortal;
            var simulation = SimulationService.Instance;
            var phase = simulation.Run.Phase;

            // A live Run has left the Town map: a click in the last of its fade does not use the portal.
            if (location == null || phase == RunPhase.Live || !simulation.CanSend(location))
                return;

            if (phase == RunPhase.Held)
                TownPortalUse.Use(fieldFace, location, simulation.Resume);
            else
                TownPortalUse.Use(fieldFace, location, () =>
                {
                    simulation.Send(location);
                    fieldFigure.ArriveByPortal();
                });
        }
    }
}
