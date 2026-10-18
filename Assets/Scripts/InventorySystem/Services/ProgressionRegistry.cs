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

        private readonly List<Quest> quests = new();

        public ProgressionRegistry(IEnumerable<Unlockable> authored)
        {
            if (authored == null)
                throw new ArgumentNullException(nameof(authored));

            var byId = new Dictionary<string, Unlockable>(StringComparer.Ordinal);

            foreach (var source in authored)
            {
                if (source == null || string.IsNullOrWhiteSpace(source.Id))
                    continue;

                if (!byId.TryAdd(source.Id, source))
                    throw new InvalidOperationException($"Two Unlockables share the id '{source.Id}'. A save could not tell them apart.");

                if (source is TownStop && HasTargets(source.UnlocksWhenComplete))
                    throw new InvalidOperationException($"The Town Stop '{source.Id}' lists Unlocks-when-Complete links, but a Town Stop never Completes.");

                _ = known.Add(source.Id);

                if (source is Quest quest)
                    quests.Add(quest);

                AddLinks(source, source.UnlocksWhenActive, whenComplete: false);
                AddLinks(source, source.UnlocksWhenComplete, whenComplete: true);
            }

            RefuseUnreachable(byId);
            RefuseLoop(byId);
        }

        /// <summary>
        /// Walks the links out from the roots (what nothing names). What it does not reach is a loop
        /// with no way in, or sits behind one: a state nothing could ever open.
        /// </summary>
        private void RefuseUnreachable(Dictionary<string, Unlockable> byId)
        {
            var reached = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<Unlockable>();

            foreach (var unlockable in byId.Values)
                if (!namedBy.ContainsKey(unlockable.Id))
                    pending.Push(unlockable);

            while (pending.Count > 0)
            {
                var current = pending.Pop();

                if (!reached.Add(current.Id))
                    continue;

                foreach (var target in TargetsOf(current))
                    if (byId.ContainsKey(target.Id))
                        pending.Push(target);
            }

            foreach (var unlockable in byId.Values)
                if (!reached.Contains(unlockable.Id))
                    throw new InvalidOperationException($"No root of the Unlock graph reaches '{unlockable.Id}'. It sits in, or behind, a loop with no way in.");
        }

        /// <summary>A loop that a root does reach: depth-first, a link back onto the path is the loop.</summary>
        private void RefuseLoop(Dictionary<string, Unlockable> byId)
        {
            var onPath = new HashSet<string>(StringComparer.Ordinal);
            var cleared = new HashSet<string>(StringComparer.Ordinal);

            foreach (var unlockable in byId.Values)
                Visit(unlockable, byId, onPath, cleared);
        }

        private void Visit(Unlockable unlockable, Dictionary<string, Unlockable> byId, HashSet<string> onPath, HashSet<string> cleared)
        {
            if (cleared.Contains(unlockable.Id))
                return;

            if (!onPath.Add(unlockable.Id))
                throw new InvalidOperationException($"The Unlock graph loops through '{unlockable.Id}'.");

            foreach (var target in TargetsOf(unlockable))
                if (byId.ContainsKey(target.Id))
                    Visit(target, byId, onPath, cleared);

            _ = onPath.Remove(unlockable.Id);
            _ = cleared.Add(unlockable.Id);
        }

        private static bool HasTargets(Unlockable[] targets)
        {
            foreach (var target in targets ?? Array.Empty<Unlockable>())
                if (target != null)
                    return true;

            return false;
        }

        private static IEnumerable<Unlockable> TargetsOf(Unlockable source)
        {
            foreach (var target in source.UnlocksWhenActive ?? Array.Empty<Unlockable>())
                if (target != null && !string.IsNullOrWhiteSpace(target.Id))
                    yield return target;

            foreach (var target in source.UnlocksWhenComplete ?? Array.Empty<Unlockable>())
                if (target != null && !string.IsNullOrWhiteSpace(target.Id))
                    yield return target;
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

        /// <summary>
        /// Takes every Quest's state before anything changes, so a Quest the event itself opens does not count
        /// it, then completes what each just-Completed id finishes, until nothing changes.
        /// </summary>
        public bool Complete(string id, Hero hero)
        {
            if (hero == null)
                throw new ArgumentNullException(nameof(hero));

            var activeBefore = new List<Quest>();

            foreach (var quest in quests)
                if (StateOf(quest, hero) == UnlockState.Active)
                    activeBefore.Add(quest);

            var changed = false;
            var justCompleted = new Queue<string>();

            if (!hero.HasCompleted(id))
            {
                hero.MarkCompleted(id);
                changed = true;
            }

            justCompleted.Enqueue(id);

            while (justCompleted.Count > 0)
            {
                var objective = justCompleted.Dequeue();

                for (var i = activeBefore.Count; i-- > 0;)
                {
                    if (activeBefore[i].ObjectiveId != objective)
                        continue;

                    hero.MarkCompleted(activeBefore[i].Id);
                    justCompleted.Enqueue(activeBefore[i].Id);
                    activeBefore.RemoveAt(i);
                    changed = true;
                }
            }

            return changed;
        }

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
