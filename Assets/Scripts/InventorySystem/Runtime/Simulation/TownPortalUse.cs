using System;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Using the hero's Town Portal (ADR-0022) from either map's marker. Both bring the Field map up and put the
    /// Run live at the portal's Location, and differ only in the command that does it: a resume or a Send.
    /// </summary>
    internal static class TownPortalUse
    {
        /// <summary>
        /// Brings the Field map up, runs <paramref name="command"/>, then selects <paramref name="location"/>'s toggle. The Field
        /// map is brought up here, as the Town Exit does, because the Town map's collapse on the Run going live is refused
        /// (it is the group's active member).
        /// </summary>
        public static void Use(FieldFacePanel fieldFace, LocationConfig location, Action command)
        {
            fieldFace.Expand();
            InventoryService.Instance.SyncContextToMap(true);

            command();

            // After the Run: the toggle follows it, so its OnToggle finds the Run already live here and commands nothing.
            foreach (var toggle in UnityEngine.Object.FindObjectsByType<LocationToggle>(FindObjectsInactive.Include))
                if (toggle.Location == location)
                    toggle.SyncToggle(true);
        }
    }
}
