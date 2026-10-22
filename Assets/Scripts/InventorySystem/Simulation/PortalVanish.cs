using System;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>The hero going through a portal (ADR-0022): shrunk and faded to nothing over a short beat before the map changes.</summary>
    public static class PortalVanish
    {
        /// <summary>How much of him is left <paramref name="elapsedSeconds"/> into a vanish of <paramref name="vanishSeconds"/>: 1 whole, 0 gone. Both his scale and his opacity follow it.</summary>
        public static float Remaining(float elapsedSeconds, float vanishSeconds) =>
            vanishSeconds > 0f ? 1f - Math.Clamp(elapsedSeconds / vanishSeconds, 0f, 1f) : 0f;
    }
}
