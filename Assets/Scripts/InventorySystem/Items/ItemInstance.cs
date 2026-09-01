using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;

namespace ToolSmiths.InventorySystem.Items
{
    /// <summary>
    /// One rolled item - this chest, with these affixes, at this rarity
    /// (<c>CONTEXT.md</c> "Item Instance"). Immutable after construction: a craft, socket
    /// or identify operation returns a <em>new</em> instance rather than mutating this one,
    /// which is what keeps the Phase 2 transaction snapshot sound - a rolled-back change
    /// cannot leak through a shared reference.
    ///
    /// The instance carries only what a roll produced. Everything a display needs beyond
    /// that - footprint, stack limit, name, colour - is resolved against the catalog
    /// through <see cref="ItemView"/>.
    /// </summary>
    public sealed class ItemInstance : IEquatable<ItemInstance>
    {
        /// <summary>Which <see cref="ItemDefinition"/> this was rolled from, by its stable string id.</summary>
        public string DefinitionId { get; }

        /// <summary>The quality tier this rolled at.</summary>
        public ItemRarity Rarity { get; }

        /// <summary>The level of the roll's source - the budget affix values were rolled against.</summary>
        public int ItemLevel { get; }

        /// <summary>Implicit, rolled and unique modifiers, already combined into one list. Read-only.</summary>
        public IReadOnlyList<CharacterStatModifier> Affixes { get; }

        public ItemInstance(string definitionId, ItemRarity rarity, int itemLevel,
            IReadOnlyList<CharacterStatModifier> affixes)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
                throw new ArgumentException("an item instance must reference a definition id", nameof(definitionId));
            if (itemLevel < 0)
                throw new ArgumentOutOfRangeException(nameof(itemLevel), itemLevel, "item level cannot be negative");

            DefinitionId = definitionId;
            Rarity = rarity;
            ItemLevel = itemLevel;

            // Defensive, immutable copy - the caller cannot reach back in and change the roll.
            var copy = affixes is null ? Array.Empty<CharacterStatModifier>() : affixes.ToArray();
            Affixes = Array.AsReadOnly(copy);
        }

        /// <summary>Flattens the instance to a Unity-free POCO for saving. See <see cref="ItemInstanceDto"/>.</summary>
        public ItemInstanceDto ToDto() => new()
        {
            definitionId = DefinitionId,
            rarity = Rarity.ToString(),
            itemLevel = ItemLevel,
            affixes = Affixes.Select(a => new AffixDto
            {
                stat = a.Stat.ToString(),
                value = a.Modifier.Value,
                type = a.Modifier.Type.ToString(),
                rangeMin = a.Modifier.Range.x,
                rangeMax = a.Modifier.Range.y,
            }).ToArray(),
        };

        /// <summary>
        /// Rebuilds an instance from its POCO form. Fails loud - a null dto, a missing
        /// definition id or an enum name that does not parse throws rather than producing a
        /// silently-wrong item.
        /// </summary>
        public static ItemInstance FromDto(ItemInstanceDto dto)
        {
            if (dto is null)
                throw new ArgumentNullException(nameof(dto));

            var rarity = ParseEnum<ItemRarity>(dto.rarity, "rarity");

            var affixes = new List<CharacterStatModifier>();
            if (dto.affixes != null)
                foreach (var a in dto.affixes)
                {
                    if (a is null)
                        throw new ArgumentException("an affix entry was null", nameof(dto));

                    var stat = ParseEnum<StatName>(a.stat, "affix stat");
                    var type = ParseEnum<StatModifierType>(a.type, "affix type");
                    // UnityEngine.Vector2Int, fully qualified: the roll-path types name no
                    // Unity type beyond the range struct StatModifier already requires.
                    var modifier = new StatModifier(new UnityEngine.Vector2Int(a.rangeMin, a.rangeMax), a.value, type);

                    affixes.Add(new CharacterStatModifier(stat, modifier));
                }

            return new ItemInstance(dto.definitionId, rarity, dto.itemLevel, affixes);
        }

        public bool Equals(ItemInstance other)
        {
            if (other is null)
                return false;
            if (ReferenceEquals(this, other))
                return true;

            if (!string.Equals(DefinitionId, other.DefinitionId, StringComparison.Ordinal))
                return false;
            if (Rarity != other.Rarity || ItemLevel != other.ItemLevel)
                return false;
            if (Affixes.Count != other.Affixes.Count)
                return false;

            for (var i = 0; i < Affixes.Count; i++)
                if (!SameAffix(Affixes[i], other.Affixes[i]))
                    return false;

            return true;
        }

        public override bool Equals(object obj) => Equals(obj as ItemInstance);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + (DefinitionId?.GetHashCode() ?? 0);
                hash = (hash * 31) + (int)Rarity;
                hash = (hash * 31) + ItemLevel;
                hash = (hash * 31) + Affixes.Count;
                return hash;
            }
        }

        /// <summary>Affix equality that compares the whole modifier - range included - unlike <c>StatModifier.Equals</c>.</summary>
        private static bool SameAffix(CharacterStatModifier a, CharacterStatModifier b) =>
            a.Stat == b.Stat
            && a.Modifier.Range == b.Modifier.Range
            && a.Modifier.Value.Equals(b.Modifier.Value)
            && a.Modifier.Type == b.Modifier.Type;

        private static TEnum ParseEnum<TEnum>(string name, string what) where TEnum : struct
        {
            if (!Enum.TryParse<TEnum>(name, ignoreCase: false, out var parsed) || !Enum.IsDefined(typeof(TEnum), parsed))
                throw new ArgumentException($"'{name}' is not a valid {typeof(TEnum).Name} ({what})");
            return parsed;
        }
    }
}
