using Submodules.Utility.Services;
using ToolSmiths.InventorySystem.Runtime.Character;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The span of play, holding the one <see cref="Hero"/> and its <see cref="World"/> at a time
    /// (ADR-0015). Read-only for now: a hero load that replaces both, and the <c>HeroLoaded</c>
    /// event views rebind on, are #114. A reader asks for <see cref="Hero"/> and <see cref="World"/>
    /// on each use and never caches them, so that swap is invisible to it.
    /// </summary>
    public interface ISession : IService
    {
        Hero Hero { get; }
        World World { get; }
    }
}
