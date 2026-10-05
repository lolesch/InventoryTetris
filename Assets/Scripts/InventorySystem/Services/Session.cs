using Submodules.Utility.Services;
using System;
using ToolSmiths.InventorySystem.Runtime.Character;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>The <see cref="ISession"/> of one boot, built by <see cref="SessionBuilder"/>.</summary>
    public sealed class Session : ISession
    {
        /// <summary>Call sites at the Unity edge read the session as <c>Session.Instance.Hero</c>.</summary>
        public static ISession Instance => ServiceLocator.Get<ISession>();

        public Session(Hero hero, World world)
        {
            Hero = hero ?? throw new ArgumentNullException(nameof(hero));
            World = world ?? throw new ArgumentNullException(nameof(world));
        }

        public Hero Hero { get; }
        public World World { get; }
    }
}
