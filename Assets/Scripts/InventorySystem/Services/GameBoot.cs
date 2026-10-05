using Submodules.Utility.Persistence;
using Submodules.Utility.Services;
using System;
using System.IO;
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
        private static void BootOnPlayEntry() => Arm(Load(), new FileSaveStore(SavesFolder()));

        /// <summary>
        /// Where the hero files live: a folder under the platform's persistent data path. The one place
        /// that path is named, so everything below takes a store and a test never touches it.
        /// </summary>
        private static string SavesFolder() => Path.Combine(Application.persistentDataPath, "saves");

        /// <summary>The root <see cref="GameConfig"/> from <c>Resources</c>. Throws, naming where it
        /// is expected, when there is none.</summary>
        public static GameConfig Load()
        {
            var config = Resources.Load<GameConfig>(GameConfig.ResourceKey);

            if (config == null)
                throw MissingConfig();

            return config;
        }

        /// <summary>
        /// <see cref="Build(GameConfig, ISaveStore)"/> over an in-memory save store: what a test builds,
        /// so no test reads or writes the real saves folder.
        /// </summary>
        public static ServiceRegistry Build(GameConfig config) => Build(config, new InMemorySaveStore());

        /// <summary>The services of one boot. Add each new service here, registered under the
        /// interface callers depend on. Throws when <paramref name="config"/> is missing or lacks
        /// something a service needs.</summary>
        /// <param name="saves">Where the hero files are kept. The composition root picks it.</param>
        public static ServiceRegistry Build(GameConfig config, ISaveStore saves)
        {
            if (config == null)
                throw MissingConfig();

            if (config.DefaultHero == null)
                throw new InvalidOperationException(
                    $"{nameof(GameConfig)}.{nameof(GameConfig.DefaultHero)} is not assigned - {nameof(GameBoot)} cannot build a Hero without it.");

            var registry = new ServiceRegistry();

            // One roll source for every draw the services make, so a test seeds it once.
            var rolls = new UnityRollSource();

            var items = new ItemService(config, rolls);
            registry.Register<IItemService>(items);

            // The Hero, then its World, in the one order (SessionBuilder).
            var session = SessionBuilder.Build(config, config.DefaultHero, items);
            registry.Register<ISession>(session);

            var inventory = new InventoryService(session, items);
            registry.Register<IInventoryService>(inventory);

            // Both Supplies start stocked, as the Vendor's and Healer's shelves were on Awake.
            inventory.RestockTownStops();

            var simulation = new SimulationService(session, items, inventory, config, rolls);
            registry.Register<ISimulationService>(simulation);

            // The heroes on disk. It reads and writes nothing until a caller asks.
            registry.Register<IHeroSaveService>(
                new HeroSaveService(session, items, config, simulation, saves, new JsonUtilitySerializer()));

            return registry;
        }

        /// <summary><see cref="Arm(GameConfig, ISaveStore)"/> over an in-memory save store, for a test.</summary>
        public static void Arm(GameConfig config) => Arm(config, new InMemorySaveStore());

        /// <summary>Builds from <paramref name="config"/>, arms the locator, installs the frame loop
        /// and hands it the simulation's tick. Builds first, so a failed build leaves nothing
        /// half-armed.</summary>
        public static void Arm(GameConfig config, ISaveStore saves)
        {
            var registry = Build(config, saves);

            ServiceLocator.Install(registry);
            GameLoop.Install();
            GameLoop.Add(registry.Get<ISimulationService>().Tick);
        }

        private static InvalidOperationException MissingConfig() => new(
            $"No {nameof(GameConfig)} is available. Expected the one root asset at Assets/Resources/{GameConfig.ResourceKey}.asset; " +
            $"{nameof(GameBoot)} loads it before the first scene and every service is built from it.");
    }
}
