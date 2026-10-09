using System;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Persistence;
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
    ///
    /// The Session it returns builds a hero load's pair the same way (#114), so a loaded Hero is
    /// never a different shape from the booted one.
    /// </summary>
    public static class SessionBuilder
    {
        public static Session Build(GameConfig config, HeroData data, IItemService items)
        {
            var (hero, world) = BuildPair(config, data, items);

            return new Session(hero, world,
                loaded => BuildPair(config, loaded, items),
                (save, locations) => BuildSavedPair(config, save, locations, items));
        }

        /// <summary>
        /// A Hero built the way the Session builds one - outfitted, template-fresh - without the World or
        /// the Session. What a save service writes for a newly created hero, so the file holds the same
        /// shape a load restores. This is the one place a starter kit is placed: a hero that is created
        /// gets it, a hero that is loaded is built without it and restored over.
        /// </summary>
        public static Hero BuildHero(GameConfig config, HeroData data, IItemService items)
        {
            var hero = BuildPair(config, data, items).Hero;
            StarterKit.Apply(hero, data, items);

            return hero;
        }

        // A saved hero is built from its template and then restored onto: retuning the template changes
        // every hero saved from it, and the pair is the same shape as a new one. A save with no template,
        // or one that is no longer authored, is built from the default template.
        private static (Hero Hero, World World, RestoreReport Report) BuildSavedPair(
            GameConfig config, HeroDto save, ILocationIndex locations, IItemService items)
        {
            var template = config?.FindHero(save?.templateId);

            if (template != null && !string.IsNullOrEmpty(save.templateId) && template.Id != save.templateId)
                Debug.LogWarning($"Hero template '{save.templateId}' is no longer authored; the hero was built from '{template.Id}' instead.");

            var (hero, world) = BuildPair(config, template, items);

            return (hero, world, HeroRestore.Restore(save, hero, locations));
        }

        private static (Hero Hero, World World) BuildPair(GameConfig config, HeroData data, IItemService items)
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
                new GroundContainer(config.GroundSize, catalog),
                new InventoryContextState());

            // 4. The Healer's side effect (issue #58): a full Health and Resource refill on every
            // genuine entry into its context. The Hero and the World are built and replaced together,
            // so the subscription lives and dies with the pair: a hero load (#114) rebinds nothing.
            // It is wired here, not by a view or by the inventory service, so a load builds it again.
            // Changed only fires on an actual change, so a re-request or the way out never heals.
            world.Context.Changed += context =>
            {
                if (context == InventoryContext.Healer)
                    hero.Heal();
            };

            return (hero, world);
        }

        // The sliders' starting positions; the live values are the hero's from here on.
        private static HeroBehaviour BehaviourDefaults(GameConfig config) => new()
        {
            SimSpeed = Mathf.Max(1f, config.SimSpeed),
            Engagement = Mathf.Max(1, config.Engagement),
            RetreatHealthFraction = Mathf.Clamp01(config.RetreatHealthFraction),
            RecallBagFillFraction = Mathf.Clamp01(config.RecallBagFillFraction),
            CastThreshold = Mathf.Clamp01(config.CastThreshold),
            OriginWeight = Mathf.Clamp01(config.OriginWeight),
            LootFilterMinimum = config.LootFilterMinimum,
        };
    }
}
