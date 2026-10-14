using Submodules.Utility.Extensions;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The narrow, read-only picture of the fight an enemy's Brain decides from: the state at the start of the tick,
    /// the same for every Brain whatever its place in the order.
    /// </summary>
    internal readonly struct EnemyView
    {
        private readonly Arena _arena;
        private readonly Enemy _self;

        public EnemyView(Arena arena, Enemy self, ICombatant hero, bool strikeReady)
        {
            _arena = arena;
            _self = self;
            Hero = hero;
            StrikeReady = strikeReady;
        }

        public Coordinate Position => _self.Position;

        public float StrikeRange => _self.StrikeRange;

        /// <summary>Whether this enemy's Strike cadence has run down. It says nothing of reach: the Encounter checks that.</summary>
        public bool StrikeReady { get; }

        /// <summary>The hero, to name as the target of an <see cref="Intent"/>. A Brain reads the members below, not this.</summary>
        public ICombatant Hero { get; }

        public Coordinate HeroPosition => _arena.HeroPosition;

        public float HeroHealthFraction => Hero.HealthFraction;

        /// <summary>Whether <paramref name="point"/> stands within <paramref name="range"/> of the hero.</summary>
        public bool InRangeOfHero(Coordinate point, float range) => _arena.InRangeOfHero(point, range);
    }
}
