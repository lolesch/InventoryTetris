using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Runtime.Simulation;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Buttons
{
    /// <summary>
    /// To Town: fades the Town face back in unconditionally (<see cref="PanelButton.OnClick"/>) -
    /// covering both a real Return and backing out of a Go Venture preview, where no Run exists
    /// yet to Return. Only Returns when <see cref="ISimulationService.CanReturn"/> -
    /// <see cref="RunState.Return"/> requires exactly that and throws otherwise, which the preview-cancel path
    /// would hit on every click without this guard. A held Run is only walked away from: it ends nothing. While a
    /// Recall's portal opens the click is refused whole, without fading the Town face in.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToTownButton : PanelButton
    {
        [SerializeField, Tooltip("The Field map's Hero figure, which stands at this Exit when the map comes up and walks out to it on a Return.")]
        private HeroFigure heroFigure;
        [SerializeField, Tooltip("Where the figure stands at the Exit. Empty: this button's own centre.")]
        private RectTransform travelPoint;

        // Announces the Exit's point to the figure.
        protected override void OnEnable()
        {
            base.OnEnable();

            if (heroFigure != null)
                heroFigure.SetExit(this.TravelPointOr(travelPoint));
        }

        protected override void OnClick()
        {
            var simulation = SimulationService.Instance;

            // A Return is refused while a Recall's portal opens (ADR-0022): the click does nothing, and the Town face
            // stays down, so the screen and the Run do not part ways.
            if (simulation.Run.IsRecalling)
                return;

            // A hero in the Field walks to the Exit first, the Run waiting for him; anywhere else there is no walk.
            if (heroFigure != null)
                heroFigure.WalkToExit(Leave);
            else
                Leave();
        }

        // He is at the Exit: the Town face comes up and the Run, if any, Returns.
        private void Leave()
        {
            var simulation = SimulationService.Instance;

            base.OnClick();

            if (simulation.CanReturn)
                _ = simulation.Return();
        }
    }
}
