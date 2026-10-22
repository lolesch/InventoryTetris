using System;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>A portal opening (ADR-0022): it waits a beat, then grows from a sliver as wide as it is tall to its full size.</summary>
    public static class PortalAppear
    {
        /// <summary>How tall the portal is when it starts to grow, as a share of its full height; it starts no wide at all.</summary>
        public const float StartHeight = 0.5f;

        /// <summary>How open it is <paramref name="elapsedSeconds"/> in, after <paramref name="delaySeconds"/> of waiting and <paramref name="growSeconds"/> of growing: 0 shut, 1 open.</summary>
        public static float Progress(float elapsedSeconds, float delaySeconds, float growSeconds)
        {
            var grown = elapsedSeconds - Math.Max(0f, delaySeconds);
            return growSeconds > 0f ? Math.Clamp(grown / growSeconds, 0f, 1f) : grown >= 0f ? 1f : 0f;
        }

        /// <summary>The portal's scale at <paramref name="progress"/>: its width from 0 to 1, its height from <see cref="StartHeight"/> to 1.</summary>
        public static (float X, float Y) Scale(float progress)
        {
            var open = Math.Clamp(progress, 0f, 1f);
            return (open, StartHeight + (1f - StartHeight) * open);
        }
    }
}
