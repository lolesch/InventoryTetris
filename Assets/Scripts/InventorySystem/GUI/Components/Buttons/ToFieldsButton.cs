using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Runtime.Simulation;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Buttons
{
    /// <summary>
    /// The Town Exit (ADR-0021): a click starts the Minimap crossfade at once and Travels the Town figure to this
    /// button. The fading Town face already takes input away, so nothing retargets the figure during the fade.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToFieldsButton : PanelButton
    {
        [SerializeField, Tooltip("The Town map's Hero figure, which walks to this Exit as the Town map fades.")]
        private TownHeroFigure heroFigure;
        [SerializeField, Tooltip("Where the figure stands at the Exit. Empty: this button's own centre.")]
        private RectTransform travelPoint;

        // Announces the Exit's point to the figure, which stands there after a Return.
        protected override void OnEnable()
        {
            base.OnEnable();

            if (heroFigure != null)
                heroFigure.SetExit(TravelPoint);
        }

        private RectTransform TravelPoint => travelPoint ? travelPoint : (RectTransform)transform;

        protected override void OnClick()
        {
            base.OnClick();
            InventoryService.Instance.SyncContextToPhase(true);

            if (heroFigure != null)
                heroFigure.TravelTo(TravelPoint);
        }
    }
}
