using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Distributions;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Services;
using UnityEditor;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// Builds a <see cref="GameConfig"/> for a test. Every property has a private setter, so the
    /// fields are written through <see cref="SerializedObject"/>, which is also how the Inspector
    /// writes them. The catalog, loot table and icons are the authored ones; the coin odds are the
    /// test's own (copper and iron, one weight each), so a currency assertion does not move when
    /// the authored odds are retuned. Everything created goes into the caller's list to destroy.
    /// </summary>
    internal static class TestGameConfig
    {
        private static readonly string[] QuantityOrder = { "NONE", "Copper", "Iron", "Silver", "Gold" };

        public static GameConfig CreateEmpty(List<Object> created)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            created.Add(config);
            return config;
        }

        public static GameConfig Create(List<Object> created)
        {
            var authored = GameBoot.Load();
            var config = CreateEmpty(created);

            var types = ScriptableObject.CreateInstance<CurrencyTypeDistribution>();
            var amounts = ScriptableObject.CreateInstance<CurrencyDropTable>();
            created.Add(types);
            created.Add(amounts);

            SetWeights(types, copper: 1u, iron: 1u);
            SetRanges(amounts, copper: new Vector2Int(4, 12), iron: new Vector2Int(10, 30));

            var so = new SerializedObject(config);
            Assign(so, "ItemTypeData", authored.ItemTypeData);
            Assign(so, "Catalog", authored.Catalog);
            Assign(so, "ItemCategoryDistribution", authored.ItemCategoryDistribution);
            Assign(so, "ItemRarityDistribution", authored.ItemRarityDistribution);
            Assign(so, "CurrencyTypeDistribution", types);
            Assign(so, "CurrencyDropTable", amounts);

            var icons = so.FindProperty("currencyIcons");
            icons.arraySize = authored.CurrencyIcons.Count;
            for (var i = 0; i < authored.CurrencyIcons.Count; i++)
                icons.GetArrayElementAtIndex(i).objectReferenceValue = authored.CurrencyIcons[i];

            _ = so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        /// <summary>Gives the test config's coin odds a fail bucket of this weight.</summary>
        public static void CurrencyFailWeight(GameConfig config, float weight)
        {
            var so = new SerializedObject(config.CurrencyTypeDistribution);
            so.FindProperty("failWeight").intValue = (int)weight;
            _ = so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetWeights(CurrencyTypeDistribution distribution, uint copper, uint iron)
        {
            var so = new SerializedObject(distribution);
            var quantities = so.FindProperty("quantities");

            for (var i = 0; i < quantities.arraySize; i++)
            {
                var name = QuantityOrder[i];
                quantities.GetArrayElementAtIndex(i).FindPropertyRelative("Quantity").intValue =
                    name == "Copper" ? (int)copper : name == "Iron" ? (int)iron : 0;
            }

            _ = so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRanges(CurrencyDropTable table, Vector2Int copper, Vector2Int iron)
        {
            var so = new SerializedObject(table);
            so.FindProperty("copper").vector2IntValue = copper;
            so.FindProperty("iron").vector2IntValue = iron;
            _ = so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Assign(SerializedObject so, string property, Object value) =>
            so.FindProperty($"<{property}>k__BackingField").objectReferenceValue = value;
    }
}
