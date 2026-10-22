using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// Binds <see cref="ISettlementBag"/> to a <see cref="Hero"/>: its Equipment and Inventory for the
    /// Corpse burial, and the same two cells for the recovery. Built over the hero the settlement
    /// is for, so a hero load needs nothing re-pointed - the next settlement is built over the new one.
    /// </summary>
    public sealed class ContainerSettlementBag : ISettlementBag
    {
        private readonly Hero _hero;

        public ContainerSettlementBag(Hero hero) =>
            _hero = hero ?? throw new ArgumentNullException(nameof(hero));

        /// <summary>
        /// One pass over the worn gear and the bag: every package becomes an entry keyed by its cell
        /// (the bag's coins among them - the Wallet is the bag's coin cells), then both are emptied. The
        /// Equipment's removal hook takes the worn gear's stats off with it.
        /// </summary>
        public IReadOnlyList<CorpseEntry> TakeEverything()
        {
            var contents = Entries(_hero.Equipment, CorpseSource.Equipment)
                .Concat(Entries(_hero.Inventory, CorpseSource.Bag))
                .ToArray();

            _hero.Equipment.RemoveAll();
            _hero.Inventory.RemoveAll();

            return contents;
        }

        /// <summary>
        /// At the remembered cell when it is free, else anywhere the item still goes: worn gear through the
        /// acquisition entry point (an empty slot, else the bag), a bag item into the bag alone, so nothing
        /// the hero carried equips itself. A save from before cells were kept (<see cref="CorpseSource.Unknown"/>)
        /// recovers as a fresh pick-up.
        /// </summary>
        public uint Restore(CorpseEntry entry)
        {
            var package = new Package(null, entry.Item, entry.Amount);
            var cell = new Vector2Int(entry.X, entry.Y);

            switch (entry.Source)
            {
                case CorpseSource.Equipment:
                    _ = _hero.Equipment.TryAddAtPosition(cell, ref package);
                    if (package.Amount > 0u)
                        _ = ItemAcquisition.TryPlace(ref package, _hero.Equipment, _hero.Inventory);
                    break;

                case CorpseSource.Bag:
                    _ = _hero.Inventory.TryAddAtPosition(cell, ref package);
                    if (package.Amount > 0u)
                        _ = _hero.Inventory.TryAddToContainer(ref package);
                    break;

                default:
                    _ = ItemAcquisition.TryPlace(ref package, _hero.Equipment, _hero.Inventory);
                    break;
            }

            return package.Amount;
        }

        private static IEnumerable<CorpseEntry> Entries(AbstractDimensionalContainer container, CorpseSource source) =>
            container.StoredPackages.Select(entry =>
                new CorpseEntry(entry.Value.Item, entry.Value.Amount, source, entry.Key.x, entry.Key.y));
    }
}
