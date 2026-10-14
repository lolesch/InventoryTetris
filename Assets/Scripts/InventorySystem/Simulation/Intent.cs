using Submodules.Utility.Extensions;

namespace ToolSmiths.InventorySystem.Simulation
{
    internal enum IntentKind
    {
        /// <summary>Walk toward the target, stopping at the fighter's own stop distance.</summary>
        Approach,
        /// <summary>Strike the target once, if the Encounter allows it.</summary>
        Strike,
        /// <summary>Hero only: walk to a point and stop on it.</summary>
        Walk,
        /// <summary>Hero only: Cast, if the Encounter allows it.</summary>
        Cast,
        /// <summary>Hero only: end the fight as a Recall, unless the hero went down this tick.</summary>
        Recall,
    }

    /// <summary>
    /// What a Brain asks of its fighter in one tick (ADR-0020). A command, not a goal; the Encounter carries it out
    /// only when the rules allow it. An Approach names its target rather than a point, and a Cast names no aim,
    /// because both are read when they run, after the walks that precede them in the tick.
    /// </summary>
    internal readonly struct Intent
    {
        private Intent(IntentKind kind, ICombatant target = null, Coordinate point = default)
        {
            Kind = kind;
            Target = target;
            Point = point;
        }

        public IntentKind Kind { get; }

        public ICombatant Target { get; }

        /// <summary>Where a <see cref="IntentKind.Walk"/> ends.</summary>
        public Coordinate Point { get; }

        public static Intent Approach(ICombatant target) => new(IntentKind.Approach, target);

        public static Intent Strike(ICombatant target) => new(IntentKind.Strike, target);

        public static Intent Walk(Coordinate point) => new(IntentKind.Walk, point: point);

        /// <summary>A Cast at the densest cluster in range, found when the Cast fires; the Encounter refuses it with nobody to catch.</summary>
        public static Intent Cast() => new(IntentKind.Cast);

        public static Intent Recall() => new(IntentKind.Recall);
    }
}
