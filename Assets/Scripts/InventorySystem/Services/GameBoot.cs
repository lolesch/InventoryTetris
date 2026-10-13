using Submodules.Utility.Bindings;
using Submodules.Utility.Persistence;
using Submodules.Utility.Services;
using System;
using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;
using UnityEngine.InputSystem;

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
        private static void BootOnPlayEntry() =>
            Boot(Load(), SaveStoreForPlay(StartFreshEachPlay), new PlayerPrefsBindingStore(BindingsKey), builtPlayer: !Application.isEditor);

        /// <summary>
        /// A Play entry over <paramref name="saves"/>: arms the services, continues the last hero and installs
        /// the quit save. Separate from the hook so a test can run it over an in-memory store.
        /// </summary>
        /// <param name="bindings">Where the player's key rebindings live; a test leaves it out and gets none.</param>
        /// <param name="builtPlayer">
        /// Whether this is a built player rather than the Editor. A built player opens on the Main Menu, whose
        /// showcase needs a hero in the Session, so it continues the last-selected hero but never creates one:
        /// with no saves the Session keeps the default-template hero, which is not from a file and is never
        /// saved, and the name is asked for on the menu. The Editor creates "Hero" on a first launch, so Play
        /// from the game scene stays a developer shortcut. A test passes <c>true</c> to take the player's path.
        /// </param>
        internal static void Boot(GameConfig config, ISaveStore saves, IBindingOverrideStore bindings = null, bool builtPlayer = false)
        {
            Arm(config, saves, bindings);

            ContinueLastHero(builtPlayer);
            GameExit.Install(ServiceLocator.Get<ISimulationService>(), ServiceLocator.Get<IHeroSaveService>());
            GameEntryGuard.Install(builtPlayer);
        }

        /// <summary>The <c>PlayerPrefs</c> key of the player's key rebindings: per machine, like <see cref="Data.UserSettings"/>.</summary>
        private const string BindingsKey = "settings.keyBindings";

        /// <summary>The <c>Resources</c> asset holding the default keys, one action per <see cref="GameAction"/>.</summary>
        public const string InputResourceKey = "GameInput";

        /// <summary>The key-spam guard: a hotkey pressed again within this long is the same press. Short
        /// enough that no deliberate double-tap is lost, long enough to swallow a key held in a bounce
        /// or a hammered one that would flicker a panel or the pause.</summary>
        private const float HotkeyRepeatSeconds = 0.15f;

        /// <summary>The <c>EditorPrefs</c> key of the Start Fresh Each Play toggle. Per machine, so it neither
        /// dirties an asset nor follows the developer to another machine.</summary>
        public const string StartFreshKey = "ToolSmiths.InventoryTetris.StartFreshEachPlay";

        /// <summary>Whether this Play entry skips the saves. Editor only: a player build has no switch.</summary>
        public static bool StartFreshEachPlay
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetBool(StartFreshKey, false);
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// The store a Play entry boots over: the saves folder, or - for Start Fresh - an empty in-memory
        /// one, so the entry reads no file, builds a new hero with its starter kit, and writes nothing.
        /// </summary>
        public static ISaveStore SaveStoreForPlay(bool startFresh) =>
            startFresh ? new InMemorySaveStore() : new FileSaveStore(SaveLocation.Folder());

        // Pressing Play, or launching, continues the last-selected hero; the Editor also creates one on a
        // first launch. A save that cannot be read must not stop the game booting: the template hero plays
        // on, and nothing is written over the files (the service saves only a hero it loaded).
        private static void ContinueLastHero(bool builtPlayer)
        {
            try
            {
                var saves = ServiceLocator.Get<IHeroSaveService>();
                _ = builtPlayer ? saves.LoadLast() : saves.LoadLastOrCreate();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not continue the last hero; starting from the default template: {exception.Message}");
            }
        }

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
        /// <param name="bindings">Where the player's key rebindings live; none by default, so a test
        /// never reads the developer's own.</param>
        public static void Arm(GameConfig config, ISaveStore saves, IBindingOverrideStore bindings = null)
        {
            var registry = Build(config, saves);

            // Not in Build: enabling actions is a global side effect, and Build touches no global.
            // The registry owns the service from here: a failed install disposes it, and so does the
            // locator's Reset, which ends this boot.
            var keys = BuildInput(bindings ?? new InMemoryBindingStore());
            registry.Register<IInputService<GameAction>>(keys);
            keys.Performed += GameInput.Raise;
            registry.Register<ICancelChain>(BuildCancel(keys, registry.Get<IInventoryService>()));

            try
            {
                ServiceLocator.Install(registry);
            }
            catch
            {
                registry.Dispose();
                throw;
            }

            GameLoop.Install();

            // The pause key subscribes to its press and lives with the service; it has no tick.
            var simulation = registry.Get<ISimulationService>();
            _ = new PauseHotkey(simulation, keys);
            GameLoop.Add(simulation.Tick);
        }

        // The Cancel chain, with the Context layer that is the services' own. The Drag layer is the drag
        // provider's and the Modal and Pause Menu layers the menus', so each registers itself.
        private static CancelChain BuildCancel(IInputService<GameAction> keys, IInventoryService inventory)
        {
            var chain = new CancelChain(keys);

            _ = chain.Register(CancelRank.Context, () =>
            {
                if (inventory.ActiveContext == InventoryContext.None)
                    return false;

                inventory.CloseContext();
                return true;
            });

            return chain;
        }

        private static InputService<GameAction> BuildInput(IBindingOverrideStore bindings)
        {
            var actions = Resources.Load<InputActionAsset>(InputResourceKey);

            return actions != null
                ? new InputService<GameAction>(actions, bindings, HotkeyRepeatSeconds)
                : throw new InvalidOperationException(
                    $"No default keys are available. Expected the input actions at Assets/Resources/{InputResourceKey}.inputactions; " +
                    $"{nameof(GameBoot)} binds every {nameof(GameAction)} from it.");
        }

        private static InvalidOperationException MissingConfig() => new(
            $"No {nameof(GameConfig)} is available. Expected the one root asset at Assets/Resources/{GameConfig.ResourceKey}.asset; " +
            $"{nameof(GameBoot)} loads it before the first scene and every service is built from it.");
    }
}
