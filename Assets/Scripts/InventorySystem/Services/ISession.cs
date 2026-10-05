using Submodules.Utility.Services;
using System;
using ToolSmiths.InventorySystem.Persistence;
using ToolSmiths.InventorySystem.Runtime.Character;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The span of play, holding the one <see cref="Hero"/> and its <see cref="World"/> at a time
    /// (ADR-0015). A hero load (<see cref="TryLoad"/>) builds both anew and swaps them as a pair, so
    /// no field of the previous hero can leak into the next. A reader asks for <see cref="Hero"/> and
    /// <see cref="World"/> on each use and never caches them; a view that has to hold something of
    /// either (a subscription, a bound container) rebinds on <see cref="HeroLoaded"/>.
    /// </summary>
    public interface ISession : IService
    {
        Hero Hero { get; }
        World World { get; }

        /// <summary>
        /// Raised once, after <see cref="Hero"/> and <see cref="World"/> are both the new pair. What a
        /// view that bound to the old pair rebinds on; the Session itself does not change when a hero
        /// loads, so there is no event for that.
        /// </summary>
        event Action HeroLoaded;

        /// <summary>
        /// Builds a Hero from <paramref name="data"/> and a World around it, and swaps both in. The
        /// rule for a live Run is to <b>refuse</b>: while the Run is <c>InField</c> this returns
        /// <c>false</c> and changes nothing, because ending it here would settle a Run (a Death's
        /// penalty, a Recall's Restock) nobody asked for. Recall first, then load. In Town, with or
        /// without a Run built yet, it succeeds. A build that throws leaves the current pair in place.
        /// </summary>
        bool TryLoad(HeroData data);

        /// <summary>
        /// The same load for a saved hero: the pair is built exactly as for a template, from the default
        /// template, and <paramref name="save"/> is restored onto the new Hero before the swap. Refuses
        /// while the Run is <c>InField</c>, returning <c>false</c>, a <c>null</c> report and changing
        /// nothing. <paramref name="report"/> is what the restore could not place - an unknown item, a
        /// package with no room - for the caller to quarantine. A restore that throws (an enum name
        /// that does not parse) leaves the current pair in place.
        /// </summary>
        /// <param name="locations">Resolves the saved Location ids; the simulation service's registry.</param>
        bool TryLoad(HeroDto save, ILocationIndex locations, out RestoreReport report);
    }
}
