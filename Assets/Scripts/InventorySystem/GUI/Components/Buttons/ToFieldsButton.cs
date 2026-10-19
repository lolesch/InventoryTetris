using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Runtime.Simulation;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ToolSmiths.InventorySystem.GUI.Components.Buttons
{
    /// <summary>
    /// The Town Exit (ADR-0021): a click Travels the Town figure to this button, and the Minimap crossfade starts
    /// once he stands there. Another walk in the meantime (a click on a stop) cancels the exit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToFieldsButton : PanelButton
    {
        [SerializeField, Tooltip("The Town map's Hero figure, which walks to this Exit as the Town map fades.")]
        private TownHeroFigure heroFigure;
        [SerializeField, Tooltip("Where the figure stands at the Exit. Empty: this button's own centre.")]
        private RectTransform travelPoint;

        // The click that is under way: he walks to where it landed on the Exit.
        private PointerEventData _click;

        // Announces the Exit's point to the figure, which stands there after a Return.
        protected override void OnEnable()
        {
            base.OnEnable();

            if (heroFigure != null)
                heroFigure.SetExit(this.TravelPointOr(travelPoint));
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            _click = eventData;

            try
            {
                base.OnPointerClick(eventData);
            }
            finally
            {
                _click = null;
            }
        }

        protected override void OnClick()
        {
            if (heroFigure == null)
                Leave();
            else if (heroFigure.TryPointerToMap(_click, (RectTransform)transform, out var clicked))
                heroFigure.WalkThen(clicked, Leave);
            else
                heroFigure.WalkThen(this.TravelPointOr(travelPoint), Leave);
        }

        // He is at the Exit: the Town map gives way to the Field map.
        private void Leave()
        {
            base.OnClick();
            InventoryService.Instance.SyncContextToMap(true);
        }
    }
}
