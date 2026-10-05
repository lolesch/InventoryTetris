using Submodules.Utility.Services;
using System;
using ToolSmiths.InventorySystem.Persistence;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>The <see cref="ISession"/> of one boot, built by <see cref="SessionBuilder"/>.</summary>
    public sealed class Session : ISession
    {
        private readonly Func<HeroData, (Hero Hero, World World)> build;
        private readonly Func<HeroDto, ILocationIndex, (Hero Hero, World World, RestoreReport Report)> buildFromSave;

        /// <summary>Call sites at the Unity edge read the session as <c>Session.Instance.Hero</c>.</summary>
        public static ISession Instance => ServiceLocator.Get<ISession>();

        /// <summary>
        /// Detach-before-attach subscribe to <see cref="HeroLoaded"/> for a view that holds something of the
        /// Hero or the World (a subscription, a bound container) and rebinds when they are replaced.
        /// Does nothing, and says so, when no Session is armed - an enable in Edit Mode - rather than
        /// throwing from the locator.
        /// </summary>
        /// <returns>Whether the subscription was made.</returns>
        public static bool TrySubscribeHeroLoaded(Action handler)
        {
            if (!ServiceLocator.IsArmed)
                return false;

            Instance.HeroLoaded -= handler;
            Instance.HeroLoaded += handler;
            return true;
        }

        /// <summary>The matching detach for <see cref="TrySubscribeHeroLoaded"/>, tolerant of nothing being armed.</summary>
        public static void UnsubscribeHeroLoaded(Action handler)
        {
            if (ServiceLocator.IsArmed)
                Instance.HeroLoaded -= handler;
        }

        /// <param name="build">How a hero load makes its pair: <see cref="SessionBuilder"/>'s order, over
        /// the config and item service this boot was built with.</param>
        /// <param name="buildFromSave">The same, for a saved hero: the builder makes the pair from the
        /// default template and restores the Dto onto it, so the Session never reads the save format.</param>
        public Session(Hero hero, World world, Func<HeroData, (Hero Hero, World World)> build,
            Func<HeroDto, ILocationIndex, (Hero Hero, World World, RestoreReport Report)> buildFromSave)
        {
            Hero = hero ?? throw new ArgumentNullException(nameof(hero));
            World = world ?? throw new ArgumentNullException(nameof(world));
            this.build = build ?? throw new ArgumentNullException(nameof(build));
            this.buildFromSave = buildFromSave ?? throw new ArgumentNullException(nameof(buildFromSave));
        }

        public Hero Hero { get; private set; }
        public World World { get; private set; }

        public event Action HeroLoaded;

        public bool TryLoad(HeroData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (IsInField)
                return false;

            // Built whole before either is swapped, so a build that throws changes nothing.
            var (hero, world) = build(data);

            Swap(hero, world);
            return true;
        }

        public bool TryLoad(HeroDto save, ILocationIndex locations, out RestoreReport report)
        {
            if (save == null)
                throw new ArgumentNullException(nameof(save));

            if (locations == null)
                throw new ArgumentNullException(nameof(locations));

            if (IsInField)
            {
                report = null;
                return false;
            }

            var (hero, world, restored) = buildFromSave(save, locations);

            Swap(hero, world);
            report = restored;
            return true;
        }

        private bool IsInField => World.Run is { Phase: RunPhase.InField };

        private void Swap(Hero hero, World world)
        {
            Hero = hero;
            World = world;

            HeroLoaded?.Invoke();
        }
    }
}
