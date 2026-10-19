using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Geometry;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The Town Portal on the Field map (ADR-0022): it opens beside the hero's figure where a Recall found him and
    /// stays while the portal opens and the Run is held. It is placed around the selected Location's anchor, as the
    /// <see cref="CorpseMarker"/> is, so it keeps its place on the map as the fight goes on without him.
    ///
    /// <para>It grows when it opens, and the hero waits for it before he walks in. Once the Run is held a click on it
    /// takes the hero to Town: he walks into the portal and vanishes, as a Recall does, and the portal stays open. Only
    /// the Town Portal resumes the Run, and the hero comes out of a portal that opens beside him the same way: it grows,
    /// he grows on top of it and walks to the fight, and it closes once he is clear. While the portal is still growing, or the hero is already on his way in, it is not a
    /// click target, so it swallows no click.</para>
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class FieldPortalMarker : PortalMarker
    {
        [SerializeField, Tooltip("Supplies the projection and the Location anchor the portal is placed around.")]
        private EnemyArena arena;
        [SerializeField, Tooltip("The Field map's Hero figure, who walks into the portal on a click.")]
        private HeroFigure heroFigure;
        [SerializeField, Tooltip("Canvas units from where the hero stood to the portal's centre.")]
        private Vector2 beside = new(70f, 0f);

        private Image _image;
        private bool _open;
        private Vector2 _offset;

        private Image Image => _image ? _image : _image = GetComponent<Image>();

        // The Run is polled, since a hero load replaces it and a Recall's opening raises no event of its own.
        private void Update()
        {
            // Selectable is [ExecuteAlways]: no services exist outside Play Mode.
            if (!Application.isPlaying)
                return;

            var run = SimulationService.Instance.Run;
            // The figure retires its own arrival a frame after the Run ends, which this reads first.
            var arriving = heroFigure.IsEmerging && run.Phase == RunPhase.Live && run.Encounter != null;
            var open = run.IsRecalling || run.Phase == RunPhase.Held || arriving;

            if (open && !_open)
            {
                // Beside the hero, for a Recall to walk him into and for him to step out of and walk away from.
                _offset = ArenaLayout.CorpseOffset(run.Encounter.HeroPosition, run.Encounter.Arena.Origin, arena.Projection) + beside;
                Grow(arriving ? ArrivalDelay : 0f);
            }
            else if (!open)
                Close();

            _open = open;
            Image.enabled = open;
            Tick();

            var usable = run.Phase == RunPhase.Held && IsOpen && !heroFigure.IsEnteringPortal && !heroFigure.HasEnteredPortal;
            Image.raycastTarget = usable;
            interactable = usable;
        }

        protected override void OnClick()
        {
            // A pointer event can land in the frame the Run stops being held, before the next Update takes the target away.
            if (SimulationService.Instance.Run.Phase == RunPhase.Held)
                heroFigure.WalkIntoPortal();
        }

        // Follows its anchor's scale and place; after the arena has placed the hero's figure, which walks into it.
        private void LateUpdate()
        {
            var anchor = arena.LocationAnchor;
            if (!_open || anchor == null)
                return;

            transform.position = anchor.TransformPoint(anchor.rect.center) + anchor.TransformVector(_offset);
        }
    }
}
