using System.Collections.Generic;
using Submodules.Utility.Extensions;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The narrow, read-only picture of the fight the hero's Brain decides from: the state at the start of the tick,
    /// the same for every Brain whatever its place in the order.
    /// </summary>
    internal readonly struct HeroView
    {
        private readonly Arena _arena;
        private readonly IHeroCombatant _hero;

        public HeroView(Arena arena, IHeroCombatant hero, IReadOnlyList<Enemy> enemies, float strikeRange, bool strikeReady, bool castBeatOpen)
        {
            _arena = arena;
            _hero = hero;
            Enemies = enemies;
            StrikeRange = strikeRange;
            StrikeReady = strikeReady;
            CastBeatOpen = castBeatOpen;
        }

        /// <summary>The living enemies, in spawn order.</summary>
        public IReadOnlyList<Enemy> Enemies { get; }

        public Coordinate Position => _arena.HeroPosition;

        /// <summary>The arena's origin: the place he calls home.</summary>
        public Coordinate Origin => _arena.Origin;

        /// <summary>How far his Strike reaches, read live.</summary>
        public float StrikeRange { get; }

        /// <summary>Whether his Strike cadence has run down. It says nothing of reach: the Encounter checks that.</summary>
        public bool StrikeReady { get; }

        /// <summary>
        /// Whether the Cast cadence is open this tick. It says nothing of Resource or aim: the Encounter checks those.
        /// A latch that arms on the pool charging up steps on every open beat, however idle.
        /// </summary>
        public bool CastBeatOpen { get; }

        public float HealthFraction => _hero.HealthFraction;

        public float ResourceFraction => _hero.ResourceFraction;

        /// <summary>Whether <paramref name="point"/> stands within <paramref name="range"/> of him.</summary>
        public bool InRange(Coordinate point, float range) => _arena.InRangeOfHero(point, range);
    }
}
