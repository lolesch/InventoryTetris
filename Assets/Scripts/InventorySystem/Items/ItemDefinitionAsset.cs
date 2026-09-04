using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
[assembly: InternalsVisibleTo("InventorySystem.Items.Tests")]

namespace ToolSmiths.InventorySystem.Items
{
    /// <summary>
    /// The authored adapter over <see cref="ItemDefinition"/> - one immutable template plus
    /// its art, edited by a designer in the inspector (<c>CONTEXT.md</c> "Item Definition").
    /// A base item and a unique are the <em>same type</em>: a unique is this asset with
    /// <see cref="IsUnique"/> set and <see cref="UniqueAffixes"/> filled, not a separate
    /// <c>AbstractItemObject</c> hierarchy.
    ///
    /// This is the one Unity-facing type in the roll module - a
    /// <see cref="ScriptableObject"/> that holds a <see cref="Sprite"/>. The roll-path types
    /// (<see cref="ItemDefinition"/>, <see cref="ItemInstance"/>, <see cref="ItemGenerator"/>)
    /// read only the interface and never touch this class; the authored fields are written
    /// once by the <c>UniquesMigration</c> editor script and thereafter by hand.
    /// </summary>
    [CreateAssetMenu(fileName = "New Item Definition", menuName = "Inventory System/Item Definition")]
    public sealed class ItemDefinitionAsset : ScriptableObject, ItemDefinition
    {
        /// <summary>
        /// The inspector-serialisable form of one <see cref="AffixSlot"/>. <see cref="AffixSlot"/>
        /// itself is a Unity-free readonly struct (it names no engine type), so the authored
        /// side keeps its own mirror and widens the <c>int</c> pair to a <see cref="Vector2Int"/>.
        /// </summary>
        [Serializable]
        public struct AuthoredAffixSlot
        {
            [SerializeField] private StatName stat;
            [SerializeField] private Vector2Int range;
            [SerializeField] private StatModifierType modifierType;

            [Tooltip("Relative pick weight when the generator draws affixes. Zero or less counts as an equal share.")]
            [SerializeField] private float weight;

            public AuthoredAffixSlot(StatName stat, Vector2Int range, StatModifierType modifierType, float weight = 1f)
            {
                this.stat = stat;
                this.range = range;
                this.modifierType = modifierType;
                this.weight = weight;
            }

            public readonly AffixSlot ToSlot() => new(stat, range.x, range.y, modifierType, weight);
        }

        [Tooltip("Stable id a saved instance references - a slug or GUID the author picks. Never the asset name, never the Unity asset GUID.")]
        [SerializeField] private string id;
        [SerializeField] private ItemCategory category = ItemCategory.Equipment;
        [SerializeField] private ItemSize footprint = ItemSize.OneByOne;
        [SerializeField] private uint baseStackLimit = 1u;

        [Header("Roll")]
        [SerializeField] private AuthoredAffixSlot[] affixPool = Array.Empty<AuthoredAffixSlot>();
        [SerializeField] private CharacterStatModifier[] implicitStats = Array.Empty<CharacterStatModifier>();

        [Min(0)]
        [Tooltip("Minimum character level to equip or use. Zero means no requirement.")]
        [SerializeField] private int requirementLevel;

        [Header("Unique")]
        [SerializeField] private bool isUnique;
        [SerializeField] private CharacterStatModifier[] uniqueAffixes = Array.Empty<CharacterStatModifier>();

        [Header("Category-specific (leave NONE where the category does not apply)")]
        [SerializeField] private EquipmentType equipmentType = EquipmentType.NONE;
        [SerializeField] private ConsumableType consumableType = ConsumableType.NONE;
        [SerializeField] private CurrencyType currencyType = CurrencyType.NONE;

        [Header("Art - adapter only, never on the roll path")]
        [SerializeField] private Sprite icon;

        // Cached read-only views. Rebuilt on OnValidate / Author. Wrapping in a
        // ReadOnlyCollection (rather than handing back the array as IReadOnlyList) keeps a
        // caller from casting back to the array and mutating the authored data, and gives
        // the returned list a public Count.
        [NonSerialized] private ReadOnlyCollection<AffixSlot> pool;
        [NonSerialized] private ReadOnlyCollection<CharacterStatModifier> implicits;
        [NonSerialized] private ReadOnlyCollection<CharacterStatModifier> uniques;

        public string Id => id;
        public ItemCategory Category => category;
        public ItemSize Footprint => footprint;
        public uint BaseStackLimit => baseStackLimit;

        public IReadOnlyList<AffixSlot> AffixPool
        {
            get
            {
                var authored = affixPool ?? Array.Empty<AuthoredAffixSlot>();
                if (pool == null || pool.Count != authored.Length)
                {
                    var slots = new AffixSlot[authored.Length];
                    for (var i = 0; i < authored.Length; i++)
                        slots[i] = authored[i].ToSlot();
                    pool = Array.AsReadOnly(slots);
                }
                return pool;
            }
        }

        public IReadOnlyList<CharacterStatModifier> ImplicitStats =>
            implicits ??= Array.AsReadOnly(implicitStats ?? Array.Empty<CharacterStatModifier>());

        public ItemRequirement Requirement => new(requirementLevel);

        public bool IsUnique => isUnique;

        public IReadOnlyList<CharacterStatModifier> UniqueAffixes =>
            uniques ??= Array.AsReadOnly(uniqueAffixes ?? Array.Empty<CharacterStatModifier>());

        public EquipmentType EquipmentType => equipmentType;
        public ConsumableType ConsumableType => consumableType;
        public CurrencyType CurrencyType => currencyType;

        /// <summary>The item's art. Lives on the adapter only - the roll path never needs a sprite.</summary>
        public Sprite Icon => icon;

        private void OnValidate() => InvalidateCaches();

        private void InvalidateCaches()
        {
            pool = null;
            implicits = null;
            uniques = null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only authoring seam. Used by the one-shot <c>UniquesMigration</c> script to
        /// write this asset's content from the pre-rework <c>AbstractItemObject</c> data, and
        /// by the item-model tests. Not part of the runtime contract - a definition is
        /// immutable once authored.
        /// </summary>
        internal void Author(
            string id, ItemCategory category, ItemSize footprint, uint baseStackLimit,
            AuthoredAffixSlot[] affixPool, CharacterStatModifier[] implicitStats, int requirementLevel,
            bool isUnique, CharacterStatModifier[] uniqueAffixes,
            EquipmentType equipmentType, ConsumableType consumableType, CurrencyType currencyType,
            Sprite icon)
        {
            this.id = id;
            this.category = category;
            this.footprint = footprint;
            this.baseStackLimit = baseStackLimit;
            this.affixPool = affixPool ?? Array.Empty<AuthoredAffixSlot>();
            this.implicitStats = implicitStats ?? Array.Empty<CharacterStatModifier>();
            this.requirementLevel = requirementLevel < 0 ? 0 : requirementLevel;
            this.isUnique = isUnique;
            this.uniqueAffixes = uniqueAffixes ?? Array.Empty<CharacterStatModifier>();
            this.equipmentType = equipmentType;
            this.consumableType = consumableType;
            this.currencyType = currencyType;
            this.icon = icon;
            InvalidateCaches();
        }
#endif
    }
}
