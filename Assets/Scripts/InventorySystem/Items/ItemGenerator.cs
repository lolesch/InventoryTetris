using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Probability;

namespace ToolSmiths.InventorySystem.Items
{
    /// <summary>
    /// Turns a <see cref="RollContext"/> into <see cref="ItemInstance"/>s - the <b>Roll</b>
    /// (<c>CONTEXT.md</c>). Replaces the ~30-method <c>GenerateRandomX</c> decision tree on
    /// <c>ItemProvider</c>: instead of a hand-unrolled switch per equipment type, a roll is
    /// "pick a category, pick a definition of that category from the catalog, roll a rarity,
    /// roll that many affixes from the definition's pool". Adding an item type is data - a
    /// new definition in the catalog - not another switch arm.
    ///
    /// Pure: no <c>ScriptableObject</c>, no singleton, no scene. The catalog and the roll
    /// source are constructor dependencies; the context is a parameter. A base item and a
    /// unique are one code path - a unique is a definition with <see cref="ItemDefinition.IsUnique"/>
    /// and a fixed <see cref="ItemDefinition.UniqueAffixes"/> list merged in after the roll.
    ///
    /// This names no Unity type via <c>using</c> - <c>UnityEngine.Vector2Int</c> appears once,
    /// fully qualified, because <see cref="StatModifier"/>'s only constructor takes one, the
    /// same concession <see cref="ItemInstance.FromDto"/> makes.
    /// </summary>
    public sealed class ItemGenerator
    {
        private readonly IItemCatalog catalog;
        private readonly IRollSource rolls;

        public ItemGenerator(IItemCatalog catalog, IRollSource rolls)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.rolls = rolls ?? throw new ArgumentNullException(nameof(rolls));
        }

        /// <summary>
        /// Rolls one item. What the roll produces and keeps is the returned instance and
        /// its affix list - the affix pick runs over a stack-allocated mask and a single
        /// reservoir slot, with no LINQ and no candidate list built along the way.
        /// </summary>
        public ItemInstance Roll(RollContext context)
        {
            if (context.Table is null)
                throw new ArgumentException("the roll context carries no loot table", nameof(context));

            var category = ProbabilityTable<ItemCategory>.Sample(context.Table.CategoryOdds, rolls.Next());
            if (category == default)
                throw new InvalidOperationException(
                    "the loot table's category odds carry no drop mass - nothing can be rolled");

            return Roll(PickDefinition(category), context);
        }

        /// <summary>
        /// Rolls one item of a <em>given</em> definition - the post-selection half of
        /// <see cref="Roll(RollContext)"/>. The rarity still comes from
        /// <see cref="RollContext.Table"/>'s rarity odds (with magic find cascaded); only the
        /// category-and-definition pick is skipped.
        /// </summary>
        public ItemInstance Roll(ItemDefinition definition, RollContext context)
        {
            if (context.Table is null)
                throw new ArgumentException("the roll context carries no loot table", nameof(context));

            var rarity = ProbabilityTable<ItemRarity>.Sample(
                RarityCascade.Apply(context.Table.RarityOdds, context.MagicFind), rolls.Next());
            if (rarity == default)
                throw new InvalidOperationException(
                    "the loot table's rarity odds carry no drop mass - nothing can be rolled");

            return Roll(definition, rarity, context.SourceLevel);
        }

        /// <summary>
        /// Rolls one item of a given definition <em>at a given rarity</em> - the innermost
        /// primitive, no loot table needed. This is what the debug UI on <c>ItemProvider</c>
        /// reaches for ("roll me a belt"): the caller has already decided the definition and
        /// the rarity. Throws when <paramref name="rarity"/> is the fail bucket
        /// (<c>NoDrop</c>) - that is not an item.
        /// </summary>
        public ItemInstance Roll(ItemDefinition definition, ItemRarity rarity, int itemLevel)
        {
            if (definition is null)
                throw new ArgumentNullException(nameof(definition));
            if (rarity == default)
                throw new ArgumentException("NoDrop is the fail bucket, not a rarity to roll at", nameof(rarity));

            var implicitStats = definition.ImplicitStats;
            var uniqueAffixes = definition.IsUnique ? definition.UniqueAffixes : null;
            var affixes = new List<CharacterStatModifier>(
                (implicitStats?.Count ?? 0) + AffixCountFor(rarity) + (uniqueAffixes?.Count ?? 0));

            // Implicit stats are guaranteed and pre-roll (CONTEXT.md "Affix").
            if (implicitStats != null)
                for (var i = 0; i < implicitStats.Count; i++)
                    affixes.Add(implicitStats[i]);

            RollAffixes(definition, rarity, affixes);

            // A unique merges its fixed list; base and unique are otherwise the same path.
            if (uniqueAffixes != null)
                for (var i = 0; i < uniqueAffixes.Count; i++)
                    affixes.Add(uniqueAffixes[i]);

            CombineSameStatModifiers(affixes);

            return new ItemInstance(definition.Id, rarity, itemLevel < 0 ? 0 : itemLevel, affixes);
        }

        /// <summary>
        /// Rolls <paramref name="count"/> items against the same context. Returns exactly
        /// that many - the bonus-drop maths (an <c>IncreasedItemQuantity</c> multiplier on
        /// the count) belongs to whatever decides how much loot an Encounter yields, not
        /// here.
        /// </summary>
        public IReadOnlyList<ItemInstance> RollLoot(RollContext context, int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, "cannot roll a negative amount of loot");

            var loot = new ItemInstance[count];
            for (var i = 0; i < count; i++)
                loot[i] = Roll(context);
            return Array.AsReadOnly(loot);
        }

        /// <summary>
        /// How many affixes a roll adds for a rarity, before <see cref="ItemDefinition.UniqueAffixes"/>
        /// and before same-stat modifiers are combined. Carried from <c>AbstractItem</c>'s
        /// equipment affix-count map; a rarity outside the map (<c>NoDrop</c>) adds none.
        /// </summary>
        public static int AffixCountFor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => 1,
            ItemRarity.Magic => 2,
            ItemRarity.Rare => 3,
            ItemRarity.Unique => 3,

            _ => 0,
        };

        /// <summary>
        /// Draws one definition of <paramref name="category"/> from the catalog by reservoir
        /// sampling - uniform, and allocation-free over the catalog's own enumeration. The
        /// per-type weighting the old distribution tree carried does not map onto
        /// definitions one-to-one; a weighted draw waits on authored selection weights.
        /// </summary>
        private ItemDefinition PickDefinition(ItemCategory category)
        {
            ItemDefinition chosen = null;
            var seen = 0;

            foreach (var candidate in catalog.OfCategory(category))
            {
                seen++;
                // Replace the reservoir with probability 1 / seen -> uniform. The roll is
                // always drawn (one per candidate), but the `chosen is null` arm takes the
                // first candidate whatever it rolled: a roll source can return exactly 1.0
                // (UnityEngine.Random.value is [0,1] inclusive) and `1.0 * 1 < 1` is false -
                // without this a one-item category would roll nothing.
                var replace = rolls.Next() * seen < 1f;
                if (replace || chosen is null)
                    chosen = candidate;
            }

            if (chosen is null)
                throw new InvalidOperationException($"the catalog has no item definitions in category {category}");

            return chosen;
        }

        /// <summary>
        /// Adds <see cref="AffixCountFor"/> affixes drawn from the definition's pool: each
        /// pick is weighted by <see cref="AffixSlot.Weight"/> and without replacement, so a
        /// stat is never rolled twice on one instance. Stops early if the pool is smaller
        /// than the target count.
        /// </summary>
        private void RollAffixes(ItemDefinition definition, ItemRarity rarity, List<CharacterStatModifier> into)
        {
            var target = AffixCountFor(rarity);
            if (target == 0)
                return;

            var pool = definition.AffixPool;
            if (pool == null || pool.Count == 0)
                return;

            Span<bool> used = stackalloc bool[pool.Count];

            for (var rolled = 0; rolled < target; rolled++)
            {
                var index = PickUnusedSlot(pool, used, rolls.Next());
                if (index < 0)
                    break; // pool exhausted

                used[index] = true;

                var slot = pool[index];
                into.Add(new CharacterStatModifier(slot.Stat, RollModifier(slot, rolls.Next())));
            }
        }

        /// <summary>A weighted index into the not-yet-used slots, or -1 when none remain.</summary>
        private static int PickUnusedSlot(IReadOnlyList<AffixSlot> pool, Span<bool> used, float roll)
        {
            var total = 0f;
            for (var i = 0; i < pool.Count; i++)
                if (!used[i])
                    total += WeightOf(pool[i]);

            if (total <= 0f)
                return -1;

            var target = roll * total;
            var cumulative = 0f;
            var lastUnused = -1;

            for (var i = 0; i < pool.Count; i++)
            {
                if (used[i])
                    continue;

                lastUnused = i;
                cumulative += WeightOf(pool[i]);
                if (target < cumulative)
                    return i;
            }

            return lastUnused; // roll landed on the far edge / floating-point drift
        }

        /// <summary>A weight of zero or less counts as an equal share (<see cref="AffixSlot"/>).</summary>
        private static float WeightOf(AffixSlot slot) => slot.Weight > 0f ? slot.Weight : 1f;

        /// <summary>
        /// A uniform integer value in the slot's inclusive range. Rarity does not scale the
        /// value yet - the old <c>ItemTypeData.StatRange</c> curve was a Unity type and is
        /// deferred with the rest of affix depth.
        /// </summary>
        private static StatModifier RollModifier(AffixSlot slot, float roll)
        {
            var low = slot.RangeMin;
            var high = slot.RangeMax;
            if (high < low)
                (low, high) = (high, low);

            var value = low + (int)(roll * (high - low + 1));
            if (value > high)
                value = high;

            return new StatModifier(new UnityEngine.Vector2Int(low, high), value, slot.ModifierType);
        }

        /// <summary>
        /// Folds modifiers of the same stat and the same type into one - implicit + rolled +
        /// unique can collide. Verbatim from <c>AbstractItem.CombineAffixesOfSameTypeAndMod</c>,
        /// which this replaces.
        /// </summary>
        private static void CombineSameStatModifiers(List<CharacterStatModifier> affixes)
        {
            for (var i = 0; i < affixes.Count; i++)
                for (var j = affixes.Count; j-- > i + 1;) // reverse: we remove as we go
                    if (affixes[i].Stat == affixes[j].Stat && affixes[i].Modifier.Type == affixes[j].Modifier.Type)
                    {
                        var range = affixes[i].Modifier.Range + affixes[j].Modifier.Range;
                        var value = affixes[i].Modifier.Value + affixes[j].Modifier.Value;

                        affixes[i] = new CharacterStatModifier(
                            affixes[i].Stat, new StatModifier(range, value, affixes[i].Modifier.Type));
                        affixes.RemoveAt(j);
                    }
        }
    }
}
