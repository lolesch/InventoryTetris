using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Runtime.Character;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// Places the authored starter kit on a newly created hero: each item is made by the item generator
    /// and placed the way a Drop is (auto-equip into an empty slot, else the Inventory), then the coins
    /// are banked in the Wallet. Nothing in the kit is required: an empty kit does nothing, an unknown
    /// definition or an item with no room is logged and skipped, and the hero stays valid either way.
    /// </summary>
    public static class StarterKit
    {
        public static void Apply(Hero hero, HeroData data, IItemService items)
        {
            if (hero == null || data == null || items == null)
                return;

            foreach (var starter in data.StarterItems)
                Place(hero, starter, items);

            // Gear raises the maximums but not the current values, so a new hero is topped up after the
            // kit is worn, and starts at full Health and Resource.
            hero.Heal();

            var coins = data.StarterCoins;
            if (coins.Total == 0u)
                return;

            if (hero.Wallet.CanDeposit(coins))
                hero.Wallet.Deposit(coins);
            else
                Debug.LogWarning($"{nameof(StarterKit)}: the Inventory has no room for the starter coins; the hero starts without them.");
        }

        private static void Place(Hero hero, HeroData.StarterItem starter, IItemService items)
        {
            ItemInstance item;

            try
            {
                item = items.Create(starter.DefinitionId, starter.Rarity == default ? ItemRarity.Common : starter.Rarity);
            }
            catch (KeyNotFoundException)
            {
                Debug.LogWarning($"{nameof(StarterKit)}: the catalog has no item '{starter.DefinitionId}'; it is left out of the kit.");
                return;
            }

            if (!hero.PickUpItem(item, starter.Amount == 0u ? 1u : starter.Amount))
                Debug.LogWarning($"{nameof(StarterKit)}: there is no room for '{starter.DefinitionId}'; the hero starts without it.");
        }
    }
}
