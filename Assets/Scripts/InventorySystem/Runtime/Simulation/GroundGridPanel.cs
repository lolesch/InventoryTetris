using ToolSmiths.InventorySystem.GUI.InventoryDisplays;
using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The floor panel's grid (epic #214): binds the Run's ground to a <see cref="GroundContainerDisplay"/>
    /// and keeps the drops' fade in step with their age. The display is hidden while no Run has a ground.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class GroundGridPanel : GroundViewPanel
    {
        [SerializeField] private GroundContainerDisplay display;

        private GroundContainer _shown;

        protected override void Refresh()
        {
            var ground = Bound?.Ground;

            if (ground != _shown)
            {
                _shown = ground;
                display.gameObject.SetActive(ground != null);

                if (ground != null)
                    display.SetupDisplay(ground);
            }

            if (ground != null)
                display.FadeByAge(ground.CellsOldestFirst());
        }
    }
}
