using System;
using ToolSmiths.InventorySystem.Data.Enums;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// One archetype's stat block as a function of source level: <c>stat = Base + PerLevel · S^Exp</c>.
    /// The exponents stay barely above linear on purpose — steeper makes the hard Location
    /// un-openable at realistic gear; the source-level gap carries the difficulty (issue-#18
    /// <c>/prototype</c> pass 2, <c>FINDINGS.md</c>).
    /// </summary>
    public readonly struct StatCurve
    {
        public readonly float Base;
        public readonly float PerLevel;
        public readonly float Exp;

        public StatCurve(float @base, float perLevel, float exp)
        {
            Base = @base;
            PerLevel = perLevel;
            Exp = exp;
        }

        public float At(int sourceLevel) => Base + PerLevel * (float)Math.Pow(sourceLevel, Exp);
    }

    /// <summary>Every stat one enemy archetype has, each already a curve over source level.</summary>
    public readonly struct EnemyArchetypeStats
    {
        public readonly StatCurve Health;
        /// <summary>Pre-mitigation Strike damage per hit.</summary>
        public readonly StatCurve Damage;
        /// <summary>Percent physical mitigation the enemy has against the hero's Strike.</summary>
        public readonly StatCurve ArmorPercent;
        /// <summary>Percent magical mitigation the enemy has against the hero's Cast.</summary>
        public readonly StatCurve MagicResistPercent;
        /// <summary>
        /// The damage type this archetype's Strike deals: Brute physical, Skirmisher magical. It picks the hero's
        /// mitigation (Armor or Magic Resist) and which of the enemy's damage stats carries <see cref="Damage"/>.
        /// </summary>
        public readonly DamageType DamageType;
        /// <summary>Strikes per second — flat, not a curve.</summary>
        public readonly float AttackSpeed;
        /// <summary>XP this body is worth, before the <c>(SourceLevel - heroLevel)</c> balance term.</summary>
        public readonly StatCurve Xp;
        /// <summary>
        /// Item rolls this body sheds on a kill, before the hero's <c>IncreasedItemQuantity</c>
        /// bonus (issue #24) — flat, not a curve. A first-ticket default; balancing per-archetype
        /// drop counts is unfixed (spec "Loot flow").
        /// </summary>
        public readonly int LootRolls;
        /// <summary>
        /// How far from the hero this body can Strike, in ground units - flat, not a curve. Melee is short;
        /// ranged is the same capability with a longer range, so it stands off (spatial-combat spec).
        /// </summary>
        public readonly float StrikeRange;
        /// <summary>Ground units walked per second while chasing the hero - flat, not a curve.</summary>
        public readonly float MovementSpeed;

        public EnemyArchetypeStats(StatCurve health, StatCurve damage, StatCurve armorPercent, StatCurve magicResistPercent, DamageType damageType, float attackSpeed, StatCurve xp, int lootRolls,
            float strikeRange, float movementSpeed)
        {
            StrikeRange = strikeRange;
            MovementSpeed = movementSpeed;
            Health = health;
            Damage = damage;
            ArmorPercent = armorPercent;
            MagicResistPercent = magicResistPercent;
            DamageType = damageType;
            AttackSpeed = attackSpeed;
            Xp = xp;
            LootRolls = lootRolls;
        }
    }

    /// <summary>
    /// The two shared archetype curve-sets for the whole MVP — one constant block, read only
    /// by source level (ADR-0010; numbers from the issue-#18 <c>/prototype</c> <c>FINDINGS.md</c>,
    /// starting points, not frozen). A Location supplies <em>only</em> its source level and
    /// which archetype it Packs; it never carries its own enemy curves.
    /// </summary>
    public static class EnemyArchetypes
    {
        public static readonly EnemyArchetypeStats Brute = new(
            health: new StatCurve(26f, 24f, 1.12f),
            damage: new StatCurve(1.0f, 0.82f, 1.0f),
            armorPercent: new StatCurve(3f, 0.9f, 1.0f),
            magicResistPercent: new StatCurve(0f, 0.4f, 1.0f),
            damageType: DamageType.PhysicalDamage,
            attackSpeed: 0.55f,
            xp: new StatCurve(5f, 3.5f, 1.0f),
            lootRolls: 1,
            strikeRange: 1.5f,
            movementSpeed: 2f);

        public static readonly EnemyArchetypeStats Skirmisher = new(
            health: new StatCurve(10f, 9f, 1.06f),
            damage: new StatCurve(0.5f, 0.5f, 1.0f),
            armorPercent: new StatCurve(0f, 0f, 1.0f),
            magicResistPercent: new StatCurve(0f, 0.8f, 1.0f),
            damageType: DamageType.MagicalDamage,
            attackSpeed: 1.6f,
            xp: new StatCurve(13f, 8f, 1.0f),
            lootRolls: 1,
            strikeRange: 6f,
            movementSpeed: 3.5f);

        public static EnemyArchetypeStats Of(EnemyArchetype archetype) =>
            archetype == EnemyArchetype.Brute ? Brute : Skirmisher;

        /// <summary>The archetype a Location trickles in singly, given the one it Packs.</summary>
        public static EnemyArchetype Other(EnemyArchetype packed) =>
            packed == EnemyArchetype.Brute ? EnemyArchetype.Skirmisher : EnemyArchetype.Brute;
    }
}
