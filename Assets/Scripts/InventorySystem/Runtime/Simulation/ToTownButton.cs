using Submodules.Utility.UI;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Field minimap action: Recalls the hero, or backs out of a Go Venture preview (issue
    /// #58 correction 2026-09-17). A plain <see cref="AbstractButton"/>, not a
    /// <see cref="RadioGroup"/> member — see <see cref="MinimapController.ToTown"/> for why.
    /// Holds the one reference in this relationship; <see cref="MinimapController"/> does not
    /// reference buttons back.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToTownButton : AbstractButton
    {
        [SerializeField] private MinimapController minimap;

        protected override void OnClick() => minimap.ToTown();
    }
}
