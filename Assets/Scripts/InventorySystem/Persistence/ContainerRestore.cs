using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Persistence
{
    /// <summary>
    /// Puts saved containers back into a fresh hero's, in two passes. <see cref="Place"/> puts each
    /// package at the cell it was saved at, through the target's own placement (so the Equipment
    /// re-applies gear stats and a two-hander takes both slots). A package that does not land is
    /// held back; <see cref="Settle"/> then gives each such package the first free Inventory cell,
    /// and reports what still has no home. The passes are separate so a displaced package never
    /// takes a cell another saved package is about to claim.
    /// </summary>
    public sealed class ContainerRestore
    {
        private readonly CharacterInventory fallback;
        private readonly List<Unplaced> unplaced = new();
        private readonly List<SkippedPackage> skipped = new();

        /// <param name="fallback">The Inventory: where a package that no longer fits its cell goes.</param>
        public ContainerRestore(CharacterInventory fallback) =>
            this.fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));

        /// <summary>
        /// Places <paramref name="dto"/>'s packages into <paramref name="target"/> at their saved
        /// cells. A null section places nothing, so a save from before a container existed loads.
        /// Items are rebuilt through the catalog overload, so a saved coin re-stamps onto the
        /// current denomination ladder. An item that cannot be rebuilt (an unknown definition, or an
        /// enum name that no longer parses) is skipped and reported.
        /// </summary>
        public void Place(SavedContainer source, ContainerDto dto, AbstractDimensionalContainer target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (dto?.packages == null)
                return;

            foreach (var entry in dto.packages)
            {
                if (entry.amount == 0u)
                {
                    skipped.Add(new SkippedPackage(source, entry, SkipReason.EmptyStack));
                    continue;
                }

                ItemInstance item;
                try
                {
                    item = ItemInstance.FromDto(entry.instance, target.Catalog);
                }
                catch (KeyNotFoundException)
                {
                    skipped.Add(new SkippedPackage(source, entry, SkipReason.UnknownDefinition));
                    continue;
                }
                catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
                {
                    // A renamed stat or a removed rarity in one item must not stop the hero loading.
                    skipped.Add(new SkippedPackage(source, entry, SkipReason.Unreadable));
                    continue;
                }

                var package = new Package(target, item, entry.amount);
                _ = target.TryAddAtPosition(new Vector2Int(entry.x, entry.y), ref package);

                if (0u < package.Amount)
                    unplaced.Add(new Unplaced(source, entry, package));
            }
        }

        /// <summary>
        /// Second pass: each held-back package goes to the first free Inventory cell, else it is
        /// reported as <see cref="SkipReason.DoesNotFit"/>. Call once, after every <see cref="Place"/>.
        /// </summary>
        public RestoreReport Settle()
        {
            foreach (var held in unplaced)
            {
                var package = held.Remaining;
                _ = fallback.TryAddToContainer(ref package);

                if (0u == package.Amount)
                    continue;

                var leftover = new PackageDto
                {
                    x = held.Entry.x,
                    y = held.Entry.y,
                    instance = held.Entry.instance,
                    amount = package.Amount,
                };
                skipped.Add(new SkippedPackage(held.Source, leftover, SkipReason.DoesNotFit));
            }

            unplaced.Clear();
            return new RestoreReport(skipped.ToArray());
        }

        private readonly struct Unplaced
        {
            public Unplaced(SavedContainer source, PackageDto entry, Package remaining)
            {
                Source = source;
                Entry = entry;
                Remaining = remaining;
            }

            public SavedContainer Source { get; }
            public PackageDto Entry { get; }
            public Package Remaining { get; }
        }
    }
}
