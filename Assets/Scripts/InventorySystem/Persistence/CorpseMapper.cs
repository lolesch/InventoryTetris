using System;
using System.Collections.Generic;
using System.Linq;
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
                items = corpse.Items.Select(item => item.ToDto()).ToArray(),
            };
        }

        /// <summary>
        /// Buries <paramref name="dto"/>'s items at the Location its id names, on the profile
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

            var saved = dto?.items ?? Array.Empty<ItemInstanceDto>();

            if (dto == null || (string.IsNullOrEmpty(dto.locationId) && saved.Length == 0))
                return new RestoreReport(skipped);

            if (!locations.TryGetProfile(dto.locationId, out var profile))
            {
                foreach (var item in saved)
                    skipped.Add(Skipped(item, SkipReason.UnknownLocation));

                return new RestoreReport(skipped);
            }

            var items = new List<ItemInstance>();

            foreach (var item in saved)
            {
                try
                {
                    items.Add(ItemInstance.FromDto(item, catalog));
                }
                catch (KeyNotFoundException)
                {
                    skipped.Add(Skipped(item, SkipReason.UnknownDefinition));
                }
                catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
                {
                    skipped.Add(Skipped(item, SkipReason.Unreadable));
                }
            }

            target.Bury(profile, items);
            return new RestoreReport(skipped);
        }

        private static SkippedPackage Skipped(ItemInstanceDto item, SkipReason reason) =>
            new(SavedContainer.Corpse, new PackageDto { instance = item, amount = 1u }, reason);
    }
}
