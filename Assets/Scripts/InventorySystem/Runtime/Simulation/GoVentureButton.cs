using Submodules.Utility.UI;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Town minimap action: switches the minimap to the Field face so the player can preview
    /// a destination (issue #58 correction 2026-09-17). A plain <see cref="AbstractButton"/>,
    /// not a <see cref="RadioGroup"/> member — see <see cref="MinimapController.GoVenture"/>
    /// for why. Holds the one reference in this relationship; <see cref="MinimapController"/>
    /// does not reference buttons back.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GoVentureButton : AbstractButton
    {
        [SerializeField] private MinimapController minimap;

        protected override void OnClick() => minimap.GoVenture();
    }
}
