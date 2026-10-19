using ToolSmiths.InventorySystem.Geometry;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The Town Portal on the Field map (ADR-0022): it opens beside the hero's figure where a Recall found him and
    /// stays while the portal opens and the Run is held. It is placed around the selected Location's anchor, as the
    /// <see cref="CorpseMarker"/> is, so it keeps its place on the map as the fight goes on without him.
    ///
    /// <para>Once the Run is held a click on it resumes the Run, as the Town Portal does. While the portal is still opening
    /// it is not a click target, so it swallows no click.</para>
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class FieldPortalMarker : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField, Tooltip("Supplies the projection and the Location anchor the portal is placed around.")]
        private EnemyArena arena;
        [SerializeField, Tooltip("Canvas units from where the hero stood to the portal's centre.")]
        private Vector2 beside = new(70f, 0f);

        private Image _image;
        private bool _open;
        private Vector2 _offset;

        private Image Image => _image ? _image : _image = GetComponent<Image>();

        // The Run is polled, since a hero load replaces it and a Recall's opening raises no event of its own.
        private void Update()
        {
            var run = SimulationService.Instance.Run;
            var open = run.IsRecalling || run.Phase == RunPhase.Held;

            if (open && !_open)
                _offset = ArenaLayout.CorpseOffset(run.Encounter.HeroPosition, run.Encounter.Arena.Origin, arena.Projection) + beside;

            _open = open;
            Image.enabled = open;
            Image.raycastTarget = run.Phase == RunPhase.Held;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var simulation = SimulationService.Instance;

            // A pointer event can land in the frame the Run stops being held, before the next Update takes the target away.
            if (eventData.button != PointerEventData.InputButton.Left || simulation.Run.Phase != RunPhase.Held)
                return;

            TownPortalUse.Use(GetComponentInParent<FieldFacePanel>(), Session.Instance.Hero.TownPortal, simulation.Resume);
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
