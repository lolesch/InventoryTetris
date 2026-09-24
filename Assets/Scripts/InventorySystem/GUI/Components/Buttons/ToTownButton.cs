using ToolSmiths.InventorySystem.Runtime.Simulation;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Buttons
{
    /// <summary>
    /// To Town: fades the Town face back in unconditionally (<see cref="PanelButton.OnClick"/>) -
    /// covering both a real Recall and backing out of a Go Venture preview, where no Run exists
    /// yet to Recall. Only Recalls when a Run is actually live
    /// (<see cref="RunPhase.InField"/>) - <see cref="RunState.Recall"/> requires exactly that and
    /// throws otherwise, which the preview-cancel path would hit on every click without this guard.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToTownButton : PanelButton
    {
        protected override void OnClick()
        {
            base.OnClick();

            if (SimulationProvider.Instance.Run.Phase == RunPhase.InField)
                SimulationProvider.Instance.Recall();
        }
    }
}
