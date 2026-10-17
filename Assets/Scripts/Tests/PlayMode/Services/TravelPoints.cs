using System.Reflection;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Services
{
    /// <summary>
    /// Where the hero figure is sent by a control of the real <c>Example</c> scene (ADR-0021): the control's serialized
    /// Travel Point when it names one, else its own rect. The controls live in the predefined assembly, so the field
    /// is read by name.
    /// </summary>
    internal static class TravelPoints
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

        /// <summary>The rect <paramref name="control"/> sends the figure to.</summary>
        public static RectTransform Of(RectTransform control)
        {
            foreach (var component in control.GetComponents<MonoBehaviour>())
            {
                var field = component.GetType().GetField("travelPoint", Fields);
                if (field?.GetValue(component) is RectTransform point && point)
                    return point;
            }

            return control;
        }

        /// <summary>The Town figure's Waypoint: its own point, else the Town Exit's.</summary>
        public static RectTransform Waypoint(MonoBehaviour figure)
        {
            var field = figure.GetType().GetField("waypoint", Fields);
            if (field?.GetValue(figure) is RectTransform point && point)
                return point;

            return Of((RectTransform)GameObject.Find("ToFieldsButton").transform);
        }
    }
}
