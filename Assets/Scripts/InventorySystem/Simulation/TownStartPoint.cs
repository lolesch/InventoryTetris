namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>How the hero came to be in Town without walking there (ADR-0021).</summary>
    public enum TownEntry
    {
        /// <summary>Entering the game with Play.</summary>
        Play,
        /// <summary>A hero load.</summary>
        Load,
        /// <summary>A Respawn after a Death.</summary>
        Respawn,
        /// <summary>A Return from the Field: he came in through the Town Exit.</summary>
        Return,
    }

    /// <summary>The marked points on the Town map the Town hero figure can stand at.</summary>
    public enum TownStand
    {
        Waypoint,
        TownExit,
    }

    /// <summary>The start-point table of the Town hero figure (<c>dev/specs/2026-10-16-hero-travel-design.md</c>).</summary>
    public static class TownStartPoint
    {
        /// <summary>The Town Exit after a Return, the Waypoint after every other way in.</summary>
        public static TownStand For(TownEntry entry) =>
            entry == TownEntry.Return ? TownStand.TownExit : TownStand.Waypoint;
    }
}
