using System;
using System.Collections.Generic;
using System.Linq;
using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Persistence
{
    /// <summary>The Corpse's round trip: <see cref="ToDto"/> out, <see cref="Restore"/> back in.</summary>
    public static class CorpseMapper
    {
        /// <summary>
        /// The Corpse as a Location id plus its item Dtos, or an empty Dto when there is none. A
        /// Corpse whose Location has no saveable id (never authored with one, or not in the index) is
        /// still written, with an empty id: the load cannot recover it by Send, but it reports and
        /// quarantines its items instead of the save silently forgetting them.
        /// </summary>
        public static CorpseDto ToDto(Corpse corpse, ILocationIndex locations)
        {
            if (corpse == null)
                throw new ArgumentNullException(nameof(corpse));

            if (locations == null)
                throw new ArgumentNullException(nameof(locations));

            if (!corpse.Exists)
                return new CorpseDto();

            return new CorpseDto
            {
                locationId = locations.TryGetId(corpse.Location, out var locationId) ? locationId : string.Empty,
                hasFall = corpse.FallPosition.HasValue,
                fallX = corpse.FallPosition?.x ?? 0f,
                fallZ = corpse.FallPosition?.z ?? 0f,
                worn = PackagesOf(corpse, CorpseSource.Equipment),
                bag = PackagesOf(corpse, CorpseSource.Bag),
                items = corpse.Entries
                    .Where(entry => entry.Source == CorpseSource.Unknown)
                    .SelectMany(entry => Enumerable.Repeat(entry.Item.ToDto(), (int)entry.Amount))
                    .ToArray(),
            };
        }

        private static PackageDto[] PackagesOf(Corpse corpse, CorpseSource source) =>
            corpse.Entries
                .Where(entry => entry.Source == source)
                .Select(entry => new PackageDto { x = entry.X, y = entry.Y, instance = entry.Item.ToDto(), amount = entry.Amount })
                .ToArray();

        /// <summary>
        /// Buries <paramref name="dto"/>'s packages at the Location its id names, on the profile
        /// <paramref name="locations"/> hands out, so the Corpse is recovered by the normal Send. An
        /// item whose definition is gone is skipped and reported. A Location id that is no longer
        /// authored, or an empty one with items, buries nothing and reports every item, so a sidecar
        /// can keep them. A null or empty Dto is no Corpse.
        /// </summary>
        public static RestoreReport Restore(CorpseDto dto, Corpse target, ILocationIndex locations, IItemCatalog catalog)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (locations == null)
                throw new ArgumentNullException(nameof(locations));

            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            var skipped = new List<SkippedPackage>();

            var saved = SavedPackages(dto);

            if (dto == null || (string.IsNullOrEmpty(dto.locationId) && saved.Count == 0))
                return new RestoreReport(skipped);

            if (!locations.TryGetProfile(dto.locationId, out var profile))
            {
                foreach (var (_, package) in saved)
                    skipped.Add(Skipped(package, SkipReason.UnknownLocation));

                return new RestoreReport(skipped);
            }

            var entries = new List<CorpseEntry>();

            foreach (var (source, package) in saved)
            {
                try
                {
                    entries.Add(new CorpseEntry(ItemInstance.FromDto(package.instance, catalog), package.amount, source, package.x, package.y));
                }
                catch (KeyNotFoundException)
                {
                    skipped.Add(Skipped(package, SkipReason.UnknownDefinition));
                }
                catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
                {
                    skipped.Add(Skipped(package, SkipReason.Unreadable));
                }
            }

            target.Bury(profile, entries, dto.hasFall ? new Coordinate(dto.fallX, dto.fallZ) : null);
            return new RestoreReport(skipped);
        }

        // Worn and bag packages keep their cells; the legacy items had none, so each is a single unplaced package.
        private static List<(CorpseSource Source, PackageDto Package)> SavedPackages(CorpseDto dto)
        {
            var saved = new List<(CorpseSource, PackageDto)>();

            if (dto == null)
                return saved;

            saved.AddRange((dto.worn ?? Array.Empty<PackageDto>()).Select(package => (CorpseSource.Equipment, package)));
            saved.AddRange((dto.bag ?? Array.Empty<PackageDto>()).Select(package => (CorpseSource.Bag, package)));
            saved.AddRange((dto.items ?? Array.Empty<ItemInstanceDto>())
                .Select(item => (CorpseSource.Unknown, new PackageDto { instance = item, amount = 1u })));

            return saved;
        }

        private static SkippedPackage Skipped(PackageDto package, SkipReason reason) =>
            new(SavedContainer.Corpse, package, reason);
    }
}
