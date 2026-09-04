using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Items
{
    /// <summary>
    /// A settable stand-in for <see cref="LootTable"/> - two probability vectors in enum
    /// order. The factory helpers pin one category and one rarity so a generator test only
    /// has the affix roll left to reason about; <see cref="AuthoredRarityOdds"/> is the
    /// real <c>Item Rarity Distribution.asset</c> weights for the magic-find invariant.
    /// </summary>
    internal sealed class FakeLootTable : LootTable
    {
        public IReadOnlyList<float> CategoryOdds { get; set; } = CategoryVector(ItemCategory.Equipment);
        public IReadOnlyList<float> RarityOdds { get; set; } = AuthoredRarityOdds();

        /// <summary>All category mass on <paramref name="category"/>; rarity per the authored table.</summary>
        public static FakeLootTable ForCategory(ItemCategory category) => new()
        {
            CategoryOdds = CategoryVector(category),
            RarityOdds = AuthoredRarityOdds(),
        };

        /// <summary>All category mass on <paramref name="category"/>, all rarity mass on <paramref name="rarity"/>.</summary>
        public static FakeLootTable Fixed(ItemCategory category, ItemRarity rarity) => new()
        {
            CategoryOdds = CategoryVector(category),
            RarityOdds = RarityVector(rarity),
        };

        /// <summary>normalize(0, 160, 80, 40, 20) - the shipped Item Rarity Distribution weights.</summary>
        public static IReadOnlyList<float> AuthoredRarityOdds() => Normalized(0f, 160f, 80f, 40f, 20f);

        /// <summary>A probability vector in <see cref="ItemCategory"/> order with all mass on one member.</summary>
        public static IReadOnlyList<float> CategoryVector(ItemCategory category) =>
            OneHot(Array.IndexOf((ItemCategory[])Enum.GetValues(typeof(ItemCategory)), category),
                Enum.GetValues(typeof(ItemCategory)).Length);

        /// <summary>A probability vector in <see cref="ItemRarity"/> order with all mass on one member.</summary>
        public static IReadOnlyList<float> RarityVector(ItemRarity rarity) =>
            OneHot(Array.IndexOf((ItemRarity[])Enum.GetValues(typeof(ItemRarity)), rarity),
                Enum.GetValues(typeof(ItemRarity)).Length);

        public static IReadOnlyList<float> Normalized(params float[] weights)
        {
            var sum = 0f;
            foreach (var w in weights)
                sum += w > 0f ? w : 0f;

            var result = new float[weights.Length];
            if (sum <= 0f)
                return result;

            for (var i = 0; i < weights.Length; i++)
                result[i] = (weights[i] > 0f ? weights[i] : 0f) / sum;
            return result;
        }

        private static float[] OneHot(int index, int length)
        {
            var result = new float[length];
            if (index >= 0 && index < length)
                result[index] = 1f;
            return result;
        }
    }
}
