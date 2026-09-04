using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Pure state: nothing here touches a live bag or the ground. <see cref="Bury"/>'s
    /// <c>bagContents</c> must already be the bag's contents only - equipped gear is a caller
    /// contract, never filtered here, because this module cannot see <c>CharacterEquipment</c>.
    /// Reading the bag, clearing it on burial, and placing the recovered Drops on the ground is
    /// the engine-side loot flow, issue #26.
    /// </summary>
    public sealed class Corpse
    {
        private ItemInstance[] _items = Array.Empty<ItemInstance>();

        /// <summary>Whether an unclaimed Corpse currently exists.</summary>
        public bool Exists => Location != null;

        /// <summary>The Location the Corpse is tagged with, or <c>null</c> while none exists.</summary>
        public EncounterProfile Location { get; private set; }

        /// <summary>The Corpse's contents, or empty while none exists.</summary>
        public IReadOnlyList<ItemInstance> Items => _items;

        /// <summary>
        /// Set aside <paramref name="bagContents"/> as the Corpse at <paramref name="location"/> -
        /// the hand-off a Death triggers. Replaces any Corpse already there outright, discarding
        /// its contents, even if <paramref name="location"/> is the same one. <c>null</c> or
        /// empty contents still create a Corpse - Death sets one aside every time, empty-handed
        /// or not.
        /// </summary>
        public void Bury(EncounterProfile location, IReadOnlyList<ItemInstance> bagContents)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));

            Location = location;
            _items = bagContents != null && bagContents.Count > 0 ? bagContents.ToArray() : Array.Empty<ItemInstance>();
        }

        /// <summary>
        /// Re-entering <paramref name="location"/>: if it is the Corpse's own, lays its contents
        /// out as <paramref name="drops"/> and clears the Corpse, returning <c>true</c>. Any other
        /// Location - or no Corpse at all - leaves it untouched and returns <c>false</c> with
        /// <paramref name="drops"/> empty.
        /// </summary>
        public bool TryRecover(EncounterProfile location, out IReadOnlyList<ItemInstance> drops)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));

            if (Location != location)
            {
                drops = Array.Empty<ItemInstance>();
                return false;
            }

            drops = _items;
            Location = null;
            _items = Array.Empty<ItemInstance>();
            return true;
        }
    }
}
