using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Persistence;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The authored Locations by stable id, and the one memoized <see cref="EncounterProfile"/> per
    /// <see cref="LocationConfig"/>. The memo is here, and not in the simulation service, so the
    /// service that runs a Location and the loader that restores a Corpse onto it read the same
    /// profile: the Corpse matches a Location by profile <em>reference</em>, and a fresh
    /// <see cref="LocationConfig.ToProfile"/> each call would defeat recovery. Profiles are
    /// immutable, so sharing one across Runs and heroes is safe.
    /// </summary>
    public sealed class LocationRegistry : ILocationRegistry
    {
        private readonly Dictionary<string, LocationConfig> byId = new(StringComparer.Ordinal);
        private readonly Dictionary<LocationConfig, EncounterProfile> profiles = new();

        /// <param name="authored">The Locations a saved id can name. One with no id is left out of the
        /// lookup, since it could never be saved; two with the same id throw, since a save would
        /// silently resolve to the wrong one.</param>
        public LocationRegistry(IEnumerable<LocationConfig> authored)
        {
            if (authored == null)
                throw new ArgumentNullException(nameof(authored));

            foreach (var location in authored)
            {
                if (location == null || string.IsNullOrWhiteSpace(location.Id))
                    continue;

                if (!byId.TryAdd(location.Id, location))
                    throw new InvalidOperationException($"Two Locations share the id '{location.Id}'. A saved Corpse could not tell them apart.");
            }
        }

        public LocationConfig Find(string id) =>
            id != null && byId.TryGetValue(id, out var location) ? location : null;

        public EncounterProfile ProfileFor(LocationConfig location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            if (!profiles.TryGetValue(location, out var profile))
                profiles[location] = profile = location.ToProfile();

            return profile;
        }

        public bool TryGetProfile(string locationId, out EncounterProfile profile)
        {
            var location = Find(locationId);
            profile = location == null ? null : ProfileFor(location);
            return profile != null;
        }

        public bool TryGetId(EncounterProfile profile, out string locationId)
        {
            foreach (var entry in profiles)
            {
                if (!ReferenceEquals(entry.Value, profile))
                    continue;

                locationId = entry.Key.Id;
                return true;
            }

            locationId = null;
            return false;
        }
    }
}
