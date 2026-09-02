using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using UnityEditor;
using UnityEngine;

namespace ToolSmiths.InventorySystem.EditorTools
{
    /// <summary>
    /// One-shot migration for Phase 1 issue #7. Reads the pre-rework authored content -
    /// the 156 <c>EquipmentObject</c> / <c>ConsumableObject</c> assets under
    /// <c>Data/Items/Uniques/**</c> and the <c>Item Type Data</c> affix pools - and writes
    /// the new authored model: one <see cref="ItemDefinitionAsset"/> per unique
    /// (<c>IsUnique</c>, its fixed affix list preserved), one base definition per concrete
    /// <see cref="EquipmentType"/> / <see cref="ConsumableType"/> / <see cref="CurrencyType"/>,
    /// and the aggregating <see cref="ItemCatalogAsset"/>.
    ///
    /// Purely additive: the old assets and <c>ItemProvider</c> wiring are left untouched and
    /// working - Phase 1's cutover (issue #8) deletes them with <c>AbstractItem</c>. Safe to
    /// re-run: existing output assets keep their <c>.meta</c> GUID and are rewritten in place.
    ///
    /// The 11 pre-rename assets that stored their affixes under a <c>&lt;Stats&gt;</c> field
    /// current Unity no longer deserialises (Chest 1, Cloak 1, Gloves 1, Helm 1, Pants 1,
    /// Shoulder 1, Shield 1, Ring 1, Book 1, Quiver 1, Potion 1) migrate with the affixes
    /// Unity can still load - empty for 8, the single readable stat for 3. Recorded and
    /// accepted on issue #7; not hand-re-authored.
    /// </summary>
    public static class UniquesMigration
    {
        private const string UniquesRoot = "Assets/Scripts/InventorySystem/Data/Items/Uniques";
        private const string OutputRoot = "Assets/Scripts/InventorySystem/Data/ItemDefinitions";
        private const string BaseFolder = OutputRoot + "/Base";
        private const string UniquesOutFolder = OutputRoot + "/Uniques";
        private const string CatalogPath = OutputRoot + "/Item Catalog.asset";

        private const uint EquipmentStackLimit = 1u;
        private const uint ConsumableStackLimit = 10u;

        // Footprints - verbatim from AbstractItem.GetDimension (the pre-rework source of truth).
        private static readonly Dictionary<EquipmentType, ItemSize> EquipmentFootprints = new()
        {
            { EquipmentType.Belt, ItemSize.TwoByOne },
            { EquipmentType.Boots, ItemSize.TwoByTwo },
            { EquipmentType.Bracers, ItemSize.TwoByTwo },
            { EquipmentType.Chest, ItemSize.TwoByThree },
            { EquipmentType.Cloak, ItemSize.TwoByTwo },
            { EquipmentType.Gloves, ItemSize.TwoByTwo },
            { EquipmentType.Helm, ItemSize.TwoByTwo },
            { EquipmentType.Pants, ItemSize.TwoByTwo },
            { EquipmentType.Shoulders, ItemSize.TwoByTwo },
            { EquipmentType.Sword, ItemSize.OneByThree },
            { EquipmentType.Bow, ItemSize.TwoByThree },
            { EquipmentType.Crossbow, ItemSize.TwoByFour },
            { EquipmentType.GreatSword, ItemSize.TwoByFour },
            { EquipmentType.Shield, ItemSize.TwoByThree },
            { EquipmentType.Quiver, ItemSize.OneByThree },
            { EquipmentType.Amulet, ItemSize.OneByOne },
            { EquipmentType.Ring, ItemSize.OneByOne },
        };

        private static readonly Dictionary<ConsumableType, ItemSize> ConsumableFootprints = new()
        {
            { ConsumableType.Arrow, ItemSize.OneByOne },
            { ConsumableType.Book, ItemSize.TwoByTwo },
            { ConsumableType.Potion, ItemSize.OneByTwo },
        };

        // Stack limits - verbatim from CurrencyItem's constructor switch.
        private static readonly Dictionary<CurrencyType, uint> CurrencyStackLimits = new()
        {
            { CurrencyType.Iron, 120u },
            { CurrencyType.Copper, 60u },
            { CurrencyType.Silver, 20u },
            { CurrencyType.Gold, 12u },
        };

        // Uniques folder name -> the type it holds.
        private static readonly Dictionary<string, EquipmentType> EquipmentFolders = new()
        {
            { "Belts", EquipmentType.Belt },
            { "Boots", EquipmentType.Boots },
            { "Bracers", EquipmentType.Bracers },
            { "Chests", EquipmentType.Chest },
            { "Cloaks", EquipmentType.Cloak },
            { "Gloves", EquipmentType.Gloves },
            { "Helmets", EquipmentType.Helm },
            { "Pants", EquipmentType.Pants },
            { "Shoulders", EquipmentType.Shoulders },
            { "Amulets", EquipmentType.Amulet },
            { "Rings", EquipmentType.Ring },
            { "Bows", EquipmentType.Bow },
            { "Crossbows", EquipmentType.Crossbow },
            { "Greatswords", EquipmentType.GreatSword },
            { "Quiver", EquipmentType.Quiver },
            { "Shields", EquipmentType.Shield },
            { "Swords", EquipmentType.Sword },
        };

        private static readonly Dictionary<string, ConsumableType> ConsumableFolders = new()
        {
            { "Arrows", ConsumableType.Arrow },
            { "Books", ConsumableType.Book },
        };

        [MenuItem("Tools/Inventory System/Migrate Uniques + Author Base Definitions")]
        public static void Run()
        {
            var typeData = LoadItemTypeData();
            if (typeData == null)
            {
                Debug.LogError("[UniquesMigration] no ItemTypeData asset found - aborting.");
                return;
            }

            EnsureFolder(OutputRoot);
            EnsureFolder(BaseFolder);
            EnsureFolder(UniquesOutFolder);

            var definitions = new List<ItemDefinitionAsset>();
            definitions.AddRange(AuthorBaseDefinitions(typeData));
            definitions.AddRange(MigrateUniques(typeData));

            BuildCatalog(definitions);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var uniques = definitions.Count(d => d.IsUnique);
            Debug.Log($"[UniquesMigration] wrote {definitions.Count} ItemDefinitionAssets " +
                      $"({definitions.Count - uniques} base, {uniques} unique) and the catalog at {CatalogPath}");
        }

        // --- Base definitions -------------------------------------------------

        private static IEnumerable<ItemDefinitionAsset> AuthorBaseDefinitions(ItemTypeData typeData)
        {
            foreach (var (type, footprint) in EquipmentFootprints)
            {
                EnsureFolder($"{BaseFolder}/Equipment");
                yield return WriteDefinition(
                    $"{BaseFolder}/Equipment", type.ToString(),
                    a => a.Author(
                        $"base.equipment.{Slug(type.ToString())}", ItemCategory.Equipment, footprint,
                        EquipmentStackLimit, PoolFor(typeData, type), Array.Empty<CharacterStatModifier>(), 0,
                        false, Array.Empty<CharacterStatModifier>(),
                        type, ConsumableType.NONE, CurrencyType.NONE, null));
            }

            foreach (var (type, footprint) in ConsumableFootprints)
            {
                EnsureFolder($"{BaseFolder}/Consumable");
                yield return WriteDefinition(
                    $"{BaseFolder}/Consumable", type.ToString(),
                    a => a.Author(
                        $"base.consumable.{Slug(type.ToString())}", ItemCategory.Consumable, footprint,
                        ConsumableStackLimit, PoolFor(typeData, type), Array.Empty<CharacterStatModifier>(), 0,
                        false, Array.Empty<CharacterStatModifier>(),
                        EquipmentType.NONE, type, CurrencyType.NONE, null));
            }

            foreach (var (type, stackLimit) in CurrencyStackLimits)
            {
                EnsureFolder($"{BaseFolder}/Currency");
                yield return WriteDefinition(
                    $"{BaseFolder}/Currency", type.ToString(),
                    a => a.Author(
                        $"base.currency.{Slug(type.ToString())}", ItemCategory.Currency, ItemSize.OneByOne,
                        stackLimit, Array.Empty<ItemDefinitionAsset.AuthoredAffixSlot>(),
                        Array.Empty<CharacterStatModifier>(), 0,
                        false, Array.Empty<CharacterStatModifier>(),
                        EquipmentType.NONE, ConsumableType.NONE, type, null));
            }
        }

        // --- Uniques --------------------------------------------------------

        private static IEnumerable<ItemDefinitionAsset> MigrateUniques(ItemTypeData typeData)
        {
            var guids = AssetDatabase.FindAssets("t:EquipmentObject t:ConsumableObject", new[] { UniquesRoot });

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var source = AssetDatabase.LoadAssetAtPath<AbstractItemObject>(path);
                if (source == null)
                    continue;

                var segments = path.Split('/');
                var assetName = System.IO.Path.GetFileNameWithoutExtension(path);
                var folder = segments.Length >= 2 ? segments[^2] : string.Empty;

                var item = source.GetItem();
                var icon = item != null ? item.Icon : null;
                var affixes = item?.Affixes != null ? item.Affixes.ToArray() : Array.Empty<CharacterStatModifier>();

                if (EquipmentFolders.TryGetValue(folder, out var equipmentType))
                {
                    EnsureFolder($"{UniquesOutFolder}/{equipmentType}");
                    yield return WriteDefinition(
                        $"{UniquesOutFolder}/{equipmentType}", assetName,
                        a => a.Author(
                            $"unique.{Slug(assetName)}", ItemCategory.Equipment,
                            EquipmentFootprints[equipmentType], EquipmentStackLimit,
                            PoolFor(typeData, equipmentType), Array.Empty<CharacterStatModifier>(), 0,
                            true, affixes,
                            equipmentType, ConsumableType.NONE, CurrencyType.NONE, icon));
                }
                else if (ConsumableFolders.TryGetValue(folder, out var consumableType) ||
                         assetName.StartsWith("Potion", StringComparison.OrdinalIgnoreCase))
                {
                    if (!ConsumableFolders.ContainsKey(folder))
                        consumableType = ConsumableType.Potion;

                    EnsureFolder($"{UniquesOutFolder}/{consumableType}");
                    yield return WriteDefinition(
                        $"{UniquesOutFolder}/{consumableType}", assetName,
                        a => a.Author(
                            $"unique.{Slug(assetName)}", ItemCategory.Consumable,
                            ConsumableFootprints[consumableType], ConsumableStackLimit,
                            PoolFor(typeData, consumableType), Array.Empty<CharacterStatModifier>(), 0,
                            true, affixes,
                            EquipmentType.NONE, consumableType, CurrencyType.NONE, icon));
                }
                else
                {
                    Debug.LogWarning($"[UniquesMigration] '{path}' sits in an unrecognised folder '{folder}' - skipped.");
                }
            }
        }

        // --- Shared helpers ------------------------------------------------

        private static ItemDefinitionAsset.AuthoredAffixSlot[] PoolFor(ItemTypeData data, EquipmentType type)
        {
            var ranges = data.GetPossibleStats(type);
            return ranges == null ? Array.Empty<ItemDefinitionAsset.AuthoredAffixSlot>() : ToSlots(ranges);
        }

        private static ItemDefinitionAsset.AuthoredAffixSlot[] PoolFor(ItemTypeData data, ConsumableType type)
        {
            var ranges = data.GetPossibleStats(type);
            return ranges == null ? Array.Empty<ItemDefinitionAsset.AuthoredAffixSlot>() : ToSlots(ranges);
        }

        private static ItemDefinitionAsset.AuthoredAffixSlot[] ToSlots(ItemTypeData.StatRange[] ranges)
        {
            var slots = new List<ItemDefinitionAsset.AuthoredAffixSlot>(ranges.Length);
            foreach (var range in ranges)
                if (range != null)
                    slots.Add(new ItemDefinitionAsset.AuthoredAffixSlot(
                        range.StatName, range.RolledRange, range.ModifierType));
            return slots.ToArray();
        }

        private static ItemDefinitionAsset WriteDefinition(string folder, string assetName, Action<ItemDefinitionAsset> author)
        {
            var path = $"{folder}/{assetName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ItemDefinitionAsset>(path);
            var isNew = asset == null;

            if (isNew)
                asset = ScriptableObject.CreateInstance<ItemDefinitionAsset>();

            // Populate before the asset exists on disk - a throw later in the run then
            // leaves no half-written .asset files behind.
            author(asset);

            if (isNew)
                AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void BuildCatalog(List<ItemDefinitionAsset> definitions)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalogAsset>(CatalogPath);
            var isNew = catalog == null;

            if (isNew)
                catalog = ScriptableObject.CreateInstance<ItemCatalogAsset>();

            catalog.SetDefinitions(definitions.OrderBy(d => d.Id, StringComparer.Ordinal));

            if (isNew)
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            EditorUtility.SetDirty(catalog);
        }

        private static ItemTypeData LoadItemTypeData()
        {
            var guids = AssetDatabase.FindAssets("t:ItemTypeData");
            return guids.Length == 0
                ? null
                : AssetDatabase.LoadAssetAtPath<ItemTypeData>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var slash = path.LastIndexOf('/');
            var parent = path[..slash];
            var leaf = path[(slash + 1)..];

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static string Slug(string value)
        {
            var lowered = value.Trim().ToLowerInvariant();
            var builder = new StringBuilder(lowered.Length);
            foreach (var c in lowered)
                builder.Append(char.IsLetterOrDigit(c) ? c : '-');
            return Regex.Replace(builder.ToString(), "-+", "-").Trim('-');
        }
    }
}
