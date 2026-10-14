namespace ToolSmiths.InventorySystem.Simulation
{
    internal enum IntentKind
    {
        /// <summary>Walk toward the target, stopping at the fighter's own stop distance.</summary>
        Approach,
        /// <summary>Strike the target once, if the Encounter allows it.</summary>
        Strike,
    }

    /// <summary>
    /// What a Brain asks of its fighter in one tick (ADR-0020). A command, not a goal; the Encounter carries it out
    /// only when the rules allow it. An Approach names its target rather than a point because the target's place
    /// is read when the walk runs, after the walks that precede it in the tick.
    /// </summary>
    internal readonly struct Intent
    {
        private Intent(IntentKind kind, ICombatant target)
        {
            Kind = kind;
            Target = target;
        }

        public IntentKind Kind { get; }

        public ICombatant Target { get; }

        public static Intent Approach(ICombatant target) => new(IntentKind.Approach, target);

        public static Intent Strike(ICombatant target) => new(IntentKind.Strike, target);
    }
}
