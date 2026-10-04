using Submodules.Utility.Services;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The config service over the authored item data (ADR-0015): loot and coin rolls, currency
    /// minting, catalog queries and the stat and currency icons. Built once from
    /// <see cref="GameConfig"/> and reached from the Unity edge as <c>ItemService.Instance</c>.
    ///
    /// It holds no reference to the Hero: the roll bonuses (magic find, item quantity) are
    /// arguments, supplied by the caller from whoever is rolling. Also the container core's
    /// <see cref="ICurrencyMinter"/> and the live kill's <see cref="ICoinDropSource"/>.
    /// </summary>
    public interface IItemService : IService, ICurrencyMinter, ICoinDropSource
    {
        /// <summary>The authored catalog. The one place a stored instance resolves back to its definition.</summary>
        IItemCatalog Catalog { get; }

        /// <summary>Pairs <paramref name="item"/> with its definition from <see cref="Catalog"/>.</summary>
        ItemView View(ItemInstance item);

        /// <summary>
        /// Rolls <paramref name="amount"/> drops plus one more per 100 of
        /// <paramref name="itemQuantity"/> against the global loot table, at <paramref name="magicFind"/>.
        /// A currency drop comes back as a rolled coin pile; everything else is a single item.
        /// </summary>
        List<Package> RollLoot(uint amount = 1u, float magicFind = 0f, float itemQuantity = 0f);

        /// <summary>Rolls a coin denomination, then a pile size for it. Invalid when no coins fell.</summary>
        Package RollCurrency();

        /// <summary>A random equipment item of any type, at <paramref name="magicFind"/>.</summary>
        ItemInstance RollEquipment(float magicFind = 0f);

        /// <summary>
        /// A random equipment item of <paramref name="type"/>, which may be a concrete type
        /// (<c>Belt</c>) or a category marker (<c>ONEHANDEDWEAPONS</c>).
        /// </summary>
        ItemInstance RollEquipment(EquipmentType type, float magicFind = 0f);

        /// <summary>A random consumable of any type, at <paramref name="magicFind"/>.</summary>
        ItemInstance RollConsumable(float magicFind = 0f);

        /// <summary>A random consumable of <paramref name="type"/>, at <paramref name="magicFind"/>.</summary>
        ItemInstance RollConsumable(ConsumableType type, float magicFind = 0f);

        /// <summary>The coin icon of a denomination, or <c>null</c> for none.</summary>
        Sprite GetIcon(CurrencyType currencyType);

        /// <summary>The icon the character and item-stat displays draw for a stat.</summary>
        Sprite GetStatIcon(StatName stat);
    }
}
