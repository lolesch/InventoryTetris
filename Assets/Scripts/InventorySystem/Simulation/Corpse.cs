using System;
using System.Collections.Generic;
using System.Linq;
using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The one persistent Corpse a Run's Death can leave behind (issue #22; ADR-0009
    /// <i>Death is corpse-recovery, not haul-forfeit</i>). <see cref="Bury"/> sets the bag's
    /// contents aside, tagged with the Location the hero fell at; a second Death before recovery
    /// replaces it outright, per the ADR ("a second Death... destroys the unclaimed one").
    /// Re-entering that Location <see cref="TryRecover"/>s it back out as Drops, clearing the
    /// Corpse - any other Location leaves it untouched.
    ///
    /// Pure state: nothing here touches a live bag or the ground. It holds whatever the hero wore
    /// and carried, coins included, each with the cell it lay in, so a recovery restores the
    /// layout; reading and clearing the hero is <c>RunSettlement</c>'s port. Recovery leftovers
    /// are buried again at the same Location, never put on the ground.
    /// </summary>
    public sealed class Corpse
    {
        private CorpseEntry[] _entries = Array.Empty<CorpseEntry>();
        private ItemInstance[] _items = Array.Empty<ItemInstance>();

        /// <summary>Whether an unclaimed Corpse currently exists.</summary>
        public bool Exists => Location != null;

        /// <summary>The Location the Corpse is tagged with, or <c>null</c> while none exists.</summary>
        public EncounterProfile Location { get; private set; }

        /// <summary>
        /// The arena position the hero went down at, in the arena's own coordinates. <c>null</c> when none
        /// was recorded (a save from before it was kept): the Corpse then lies at the arena's Origin.
        /// </summary>
        public Coordinate? FallPosition { get; private set; }

        /// <summary>The Corpse's packages with the cell each lay in, or empty while none exists.</summary>
        public IReadOnlyList<CorpseEntry> Entries => _entries;

        /// <summary>The item of each of <see cref="Entries"/>, or empty while none exists.</summary>
        public IReadOnlyList<ItemInstance> Items => _items;

        /// <summary>
        /// Set aside <paramref name="contents"/> as the Corpse at <paramref name="location"/> -
        /// the hand-off a Death triggers. Replaces any Corpse already there outright, discarding
        /// its contents, even if <paramref name="location"/> is the same one. <c>null</c> or
        /// empty contents still create a Corpse - Death sets one aside every time, empty-handed
        /// or not. <paramref name="fell"/> is where the hero went down, or <c>null</c> for none known.
        /// </summary>
        public void Bury(EncounterProfile location, IReadOnlyList<CorpseEntry> contents, Coordinate? fell = null)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));

            Location = location;
            FallPosition = fell;
            _entries = contents != null && contents.Count > 0 ? contents.ToArray() : Array.Empty<CorpseEntry>();
            _items = _entries.Select(entry => entry.Item).ToArray();
            Changed?.Invoke();
        }

        /// <summary>
        /// <see cref="Bury(EncounterProfile, IReadOnlyList{CorpseEntry}, Coordinate?)"/> for items with no remembered
        /// place: one each, laid back as fresh pick-ups.
        /// </summary>
        public void BuryItems(EncounterProfile location, IReadOnlyList<ItemInstance> items, Coordinate? fell = null) =>
            Bury(location, items?.Select(item => new CorpseEntry(item, 1u)).ToArray(), fell);

        /// <summary>Raised after the Corpse was buried, replaced or recovered, so a marker repaints on change.</summary>
        public event Action Changed;

        /// <summary>
        /// Re-entering <paramref name="location"/>: if it is the Corpse's own, lays its contents
        /// out as <paramref name="drops"/> and clears the Corpse, returning <c>true</c>. Any other
        /// Location - or no Corpse at all - leaves it untouched and returns <c>false</c> with
        /// <paramref name="drops"/> empty.
        /// </summary>
        public bool TryRecover(EncounterProfile location, out IReadOnlyList<CorpseEntry> drops)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));

            if (Location != location)
            {
                drops = Array.Empty<CorpseEntry>();
                return false;
            }

            drops = _entries;
            Location = null;
            FallPosition = null;
            _entries = Array.Empty<CorpseEntry>();
            _items = Array.Empty<ItemInstance>();
            Changed?.Invoke();
            return true;
        }
    }
}
