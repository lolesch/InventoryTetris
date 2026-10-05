using Submodules.Utility.Services;
using System;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// Builds every game-wide service from <see cref="GameConfig"/> and arms the
    /// <see cref="ServiceLocator"/> before the first scene loads (ADR-0015). The hook runs on every
    /// Play entry, including with domain reload disabled, after the locator's own
    /// <c>SubsystemRegistration</c> reset, so each entry is a clean boot. A missing
    /// <see cref="GameConfig"/> throws here, on the first Play, instead of surfacing as a loot roll
    /// that quietly returns nothing.
    ///
    /// <see cref="Build"/> is the test seam: it needs no scene, creates no object and touches no
    /// global, so a test builds the services from a test config and drives them directly.
    /// </summary>
    public static class GameBoot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BootOnPlayEntry() => Arm(Load());

        /// <summary>The root <see cref="GameConfig"/> from <c>Resources</c>. Throws, naming where it
        /// is expected, when there is none.</summary>
        public static GameConfig Load()
        {
            var config = Resources.Load<GameConfig>(GameConfig.ResourceKey);

            if (config == null)
                throw MissingConfig();

            return config;
        }

        /// <summary>The services of one boot. Add each new service here, registered under the
        /// interface callers depend on. Throws when <paramref name="config"/> is missing or lacks
        /// something a service needs.</summary>
        public static ServiceRegistry Build(GameConfig config)
        {
            if (config == null)
                throw MissingConfig();

            if (config.DefaultHero == null)
                throw new InvalidOperationException(
                    $"{nameof(GameConfig)}.{nameof(GameConfig.DefaultHero)} is not assigned - {nameof(GameBoot)} cannot build a Hero without it.");

            var registry = new ServiceRegistry();

            var items = new ItemService(config, new UnityRollSource());
            registry.Register<IItemService>(items);

            // The Hero, then its World, in the one order (SessionBuilder).
            var session = SessionBuilder.Build(config, config.DefaultHero, items);
            registry.Register<ISession>(session);

            var inventory = new InventoryService(session, items);
            registry.Register<IInventoryService>(inventory);

            // Both Supplies start stocked, as the Vendor's and Healer's shelves were on Awake.
            inventory.RestockTownStops();

            return registry;
        }

        /// <summary>Builds from <paramref name="config"/>, arms the locator and installs the frame
        /// loop. Builds first, so a failed build leaves nothing half-armed.</summary>
        public static void Arm(GameConfig config)
        {
            var registry = Build(config);

            ServiceLocator.Install(registry);
            GameLoop.Install();
        }

        private static InvalidOperationException MissingConfig() => new(
            $"No {nameof(GameConfig)} is available. Expected the one root asset at Assets/Resources/{GameConfig.ResourceKey}.asset; " +
            $"{nameof(GameBoot)} loads it before the first scene and every service is built from it.");
    }
}
