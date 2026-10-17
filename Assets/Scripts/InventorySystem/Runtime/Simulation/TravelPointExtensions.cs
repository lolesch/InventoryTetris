using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>The one rule for where a control sends a hero figure (ADR-0021).</summary>
    public static class TravelPointExtensions
    {
        /// <summary>The rect <paramref name="control"/> sends the figure to: the Travel Point it names, else its own rect.</summary>
        public static RectTransform TravelPointOr(this Component control, RectTransform named) =>
            named ? named : (RectTransform)control.transform;
    }
}
