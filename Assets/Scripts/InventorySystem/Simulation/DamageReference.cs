using System;
using ToolSmiths.InventorySystem.Data.Enums;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The reference maximum a damage number's size is read against (issue #213): the best raw hit of a damage type
    /// the dealer can land, with the damage spread (<see cref="EncounterTuning.DamageSpread"/>) applied, so the top
    /// of the spread is the biggest number there is. Pure, and read when a number is shown, so a re-geared hero or a
    /// changed spread moves it on the spot.
    /// </summary>
    public static class DamageReference
    {
        /// <summary>The hero's hit of <paramref name="type"/>: his physical or magical damage stat, at the top of the spread.</summary>
        public static float OfHero(IHeroCombatant hero, DamageType type, float spread) =>
            (type == DamageType.MagicalDamage ? hero.MagicalDamage : hero.PhysicalDamage) * (1f + spread);

        /// <summary>
        /// The strongest hit of <paramref name="type"/> an enemy of <paramref name="profile"/>'s Location can land: the
        /// best of the archetypes it can field (a roster that can roll at least one) whose Strike is that type, read
        /// off the archetype's curve at the Location's source level. 0 when none can - the Location cannot hit the
        /// hero with that type at all.
        /// </summary>
        public static float OfEnemies(EncounterProfile profile, DamageType type, float spread) =>
            Math.Max(OfArchetype(profile, EnemyArchetype.Brute, type, spread),
                OfArchetype(profile, EnemyArchetype.Skirmisher, type, spread));

        private static float OfArchetype(EncounterProfile profile, EnemyArchetype archetype, DamageType type, float spread)
        {
            var stats = EnemyArchetypes.Of(archetype);
            if (stats.DamageType != type || profile.RosterFor(archetype).Max < 1)
                return 0f;

            return stats.Damage.At(profile.SourceLevel) * (1f + spread);
        }
    }
}
