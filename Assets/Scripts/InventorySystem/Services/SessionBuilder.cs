using System;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The one entry point that builds a Hero and then its World, in the one order that works
    /// (ADR-0015): the hero first, then what the hero owns, then the World. The order is not a
    /// convention. The Equipment takes the hero as its stat receiver, so the hero has to exist
    /// before it; the Wallet is backed by the hero's Inventory, so that comes first too; and the
    /// World holds nothing the hero needs, so it is built last. Nothing in here calls a locator
    /// or finds anything in a scene, so a test builds the lot from a test config and fakes.
    /// </summary>
    public static class SessionBuilder
    {
        public static Session Build(GameConfig config, HeroData data, IItemService items)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (data == null)
                throw new ArgumentNullException(nameof(data), $"No {nameof(HeroData)} to build the Hero from.");

            if (items == null)
                throw new ArgumentNullException(nameof(items));

            var catalog = items.Catalog;

            // 1. The hero: its stats and resources, nothing it carries yet.
            var hero = new Hero(data);

            // 2. What the hero owns, in dependency order.
            var equipment = new CharacterEquipment(config.EquipmentSize, catalog, hero);
            var inventory = new CharacterInventory(config.InventorySize, catalog);
            var stash = new CharacterInventory(config.StashSize, catalog);
            var wallet = new Wallet(inventory, items);
            hero.Outfit(equipment, inventory, stash, wallet, BehaviourDefaults(config));

            // 3. The World, after the hero.
            var world = new World(
                new CharacterInventory(config.SupplySize, catalog),
                new CharacterInventory(config.SupplySize, catalog),
                new SoldContainer(config.SoldSize, catalog),
                new InventoryContextState());

            return new Session(hero, world);
        }

        // The sliders' starting positions; the live values are the hero's from here on.
        private static HeroBehaviour BehaviourDefaults(GameConfig config) => new()
        {
            SimSpeed = Mathf.Max(1f, config.SimSpeed),
            Engagement = Mathf.Max(1, config.Engagement),
            RetreatHealthFraction = Mathf.Clamp01(config.RetreatHealthFraction),
            RecallBagFillFraction = Mathf.Clamp01(config.RecallBagFillFraction),
            CastThreshold = Mathf.Clamp01(config.CastThreshold),
            LootFilterMinimum = config.LootFilterMinimum,
        };
    }
}
