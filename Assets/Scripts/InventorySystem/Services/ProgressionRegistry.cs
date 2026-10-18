using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Runtime.Character;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// Indexes the authored <see cref="Unlockable"/>s by id and builds the reverse links (what names each
    /// one) once, so a state is derived from the hero's Completed set without walking the assets.
    /// </summary>
    public sealed class ProgressionRegistry : IProgressionRegistry
    {
        private readonly struct Link
        {
            public readonly Unlockable Source;
            public readonly bool WhenComplete;

            public Link(Unlockable source, bool whenComplete)
            {
                Source = source;
                WhenComplete = whenComplete;
            }
        }

        private readonly Dictionary<string, List<Link>> namedBy = new(StringComparer.Ordinal);

        private readonly HashSet<string> known = new(StringComparer.Ordinal);

        public ProgressionRegistry(IEnumerable<Unlockable> authored)
        {
            if (authored == null)
                throw new ArgumentNullException(nameof(authored));

            foreach (var source in authored)
            {
                if (source == null || string.IsNullOrWhiteSpace(source.Id))
                    continue;

                _ = known.Add(source.Id);

                AddLinks(source, source.UnlocksWhenActive, whenComplete: false);
                AddLinks(source, source.UnlocksWhenComplete, whenComplete: true);
            }
        }

        private void AddLinks(Unlockable source, Unlockable[] targets, bool whenComplete)
        {
            foreach (var target in targets ?? Array.Empty<Unlockable>())
            {
                if (target == null || string.IsNullOrWhiteSpace(target.Id))
                    continue;

                if (!namedBy.TryGetValue(target.Id, out var links))
                    namedBy[target.Id] = links = new List<Link>();

                links.Add(new Link(source, whenComplete));
            }
        }

        public bool Knows(string id) => id != null && known.Contains(id);

        public UnlockState StateOf(Unlockable unlockable, Hero hero)
        {
            if (unlockable == null)
                throw new ArgumentNullException(nameof(unlockable));
            if (hero == null)
                throw new ArgumentNullException(nameof(hero));

            if (hero.HasCompleted(unlockable.Id))
                return UnlockState.Complete;

            // A root: nothing names it, so it is open from the start.
            if (unlockable.Id == null || !namedBy.TryGetValue(unlockable.Id, out var links))
                return UnlockState.Active;

            var sourceReached = false;

            foreach (var link in links)
            {
                var source = StateOf(link.Source, hero);

                if (source >= (link.WhenComplete ? UnlockState.Complete : UnlockState.Active))
                    return UnlockState.Active;

                sourceReached |= source >= UnlockState.Active;
            }

            return sourceReached ? UnlockState.Locked : UnlockState.Hidden;
        }
    }
}
