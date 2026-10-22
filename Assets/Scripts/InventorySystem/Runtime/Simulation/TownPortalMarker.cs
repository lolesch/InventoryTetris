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
    /// <para>When the hero comes through from the Field map the portal waits a beat, grows, and he steps out of it.
    /// A click uses it, and using it closes it. The hero walks to the portal at his own pace and vanishes into it,
    /// and only then does the Field map come up: with the Run held it resumes it, live exactly where it was held and
    /// with the portal's Location selected, no new fight; with no Run it Sends a fresh Encounter at the portal's
    /// Location, the figure arriving through the portal. The Field map is brought up here, as the Town Exit does,
    /// because the Town map's collapse on the Run going live is refused (it is the group's active member).</para>
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class TownPortalMarker : PortalMarker
    {
        [SerializeField, Tooltip("The Field map's face, which the portal brings up.")]
        private FieldFacePanel fieldFace;
        [SerializeField, Tooltip("The Field map's Hero figure, which arrives through the portal when a Run is Sent from it.")]
        private HeroFigure fieldFigure;
        [SerializeField, Tooltip("The Town map's Hero figure, which walks into the portal before the map changes.")]
        private TownHeroFigure townFigure;
        [SerializeField, Tooltip("Canvas units from the portal's centre to where the hero stands once he has come out of it.")]
        private Vector2 beside = new(70f, 0f);

        private Image _image;
        private bool _using;
        private bool _open;
        private LocationConfig _resuming;

        /// <summary>Canvas units from the portal's centre to where the hero stands once he has come out of it.</summary>
        public Vector2 Beside => beside;

        private Image Image => _image ? _image : _image = GetComponent<Image>();

        // Selectable is [ExecuteAlways]: it subscribes in Play Mode only.
        protected override void OnEnable()
        {
            base.OnEnable();

            if (Application.isPlaying && fieldFigure)
                fieldFigure.PortalEntered += OnHeroCameThrough;

            // Detach first, so a re-enable cannot subscribe twice.
            GameInput.Performed -= OnAction;
            GameInput.Performed += OnAction;
        }

        protected override void OnDisable()
        {
            // Not Arrived: the Town map collapses as the Field map comes up, and the held Run still waits on it.
            if (fieldFigure)
                fieldFigure.PortalEntered -= OnHeroCameThrough;

            GameInput.Performed -= OnAction;

            base.OnDisable();
        }

        // The Recall key in Town walks the hero into the open portal, as a click on it does.
        private void OnAction(GameAction action)
        {
            if (action == GameAction.Recall && IsInteractable())
                OnClick();
        }

        // The Town map is about to come up with him on it: the portal starts shut and grows once it is up.
        private void OnHeroCameThrough() => Grow(ArrivalDelay);

        private void Update()
        {
            // Selectable is [ExecuteAlways]: no services exist outside Play Mode.
            if (!Application.isPlaying)
                return;

            // Another walk cancels the one into the portal, and with it the call that uses it.
            if (_using && !townFigure.IsEnteringPortal)
                _using = false;

            var open = Session.Instance.Hero.TownPortal != null;
            if (open && !_open)
                OpenAtOnce();
            else if (!open)
            {
                _using = false;
                Close();
            }

            _open = open;
            Image.enabled = open;
            Tick();
            interactable = open && IsOpen && !_using;
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

            // Before the Field map comes up, so the figure stays gone until he steps out of the portal there.
            fieldFigure.ArriveByPortal();
            fieldFace.Expand();
            InventoryService.Instance.SyncContextToMap(true);

            if (simulation.Run.Phase == RunPhase.Held)
            {
                // The Run stays held until he has come out of the portal and walked clear of it.
                _resuming = location;
                fieldFigure.Arrived += OnHeroArrived;
                return;
            }

            simulation.Send(location);
            Session.Instance.Hero.TownPortal = null;

            // The fresh Run waits for him too: he is not on the Field map yet.
            simulation.SetHeroInTransit(true);
            SyncToggle(location);
        }

        // He is out and clear: the held Run goes on, live exactly where it was held.
        private void OnHeroArrived()
        {
            fieldFigure.Arrived -= OnHeroArrived;

            var simulation = SimulationService.Instance;
            var location = _resuming;
            _resuming = null;

            // The Field map was left by its Exit while he came out: the Run stays held, with the portal open, as any Exit from a held Run.
            if (location == null || simulation.Run.Phase != RunPhase.Held || !fieldFace.IsInteractive)
                return;

            simulation.Resume();
            SyncToggle(location);
        }

        // After the Run: the toggle follows it, so its OnToggle finds the Run already live here and commands nothing.
        private static void SyncToggle(LocationConfig location)
        {
            // A cleared Location's toggle stays off, as it let go on the clear: he arrives and the player chooses what comes
            // next. Turning it on would send him to the Location again.
            if (SimulationService.Instance.Run.Encounter is { Phase: SimulationPhase.Complete })
                return;

            foreach (var toggle in FindObjectsByType<LocationToggle>(FindObjectsInactive.Include))
                if (toggle.Location == location)
                    toggle.SyncToggle(true);
        }
    }
}
