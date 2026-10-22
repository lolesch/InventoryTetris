using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    /// <summary>
    /// What reaching a level does to the hero, as pure functions of the level. The one place the
    /// rule is written: the level-up path calls it as the hero climbs, and a loader calls it to put a
    /// saved level back, so a load never replays the XP gain that earned it.
    /// </summary>
    public static class LevelProgression
    {
        /// <summary>
        /// The modifier reaching <paramref name="level"/> adds to the Experience resource's maximum:
        /// the XP bar grows with every level. A hero at level N carries the modifiers of levels 2..N.
        /// </summary>
        public static StatModifier ExperienceThresholdModifier(uint level) =>
            new(new Vector2Int(0, int.MaxValue), level * 100 + 80);

        // Placeholders: #349 fixes the numbers.
        private static readonly (StatName Stat, float Value)[] Growth =
        {
            (StatName.Health, 25f),
            (StatName.Resource, 10f),
            (StatName.PhysicalDamage, 2f),
            (StatName.MagicalDamage, 2f),
        };

        /// <summary>
        /// The flat stat modifiers each level reached adds: survival and damage come from levels and gear, never
        /// from a heal. A hero at level N carries them N-1 times, applied by the level-up and by a restore alike.
        /// </summary>
        public static IReadOnlyList<CharacterStatModifier> LevelGrowth { get; } =
            Growth.Select(growth => new CharacterStatModifier(growth.Stat, new StatModifier(new Vector2Int(0, int.MaxValue), growth.Value))).ToArray();
    }
}
