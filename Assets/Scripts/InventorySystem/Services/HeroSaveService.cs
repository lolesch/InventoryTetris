using Submodules.Utility.Persistence;
using Submodules.Utility.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Persistence;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The <see cref="IHeroSaveService"/> over a save store: one slot per hero, keyed by its generated
    /// id, and one for the Account. The store is injected, so the persistent data path is chosen
    /// once, at boot, and a test runs over an in-memory store.
    /// </summary>
    public sealed class HeroSaveService : IHeroSaveService
    {
        private const string AccountKey = "account";
        private const string FirstHeroName = "Hero";
        private const int HeroSchemaVersion = 1;
        private const int AccountSchemaVersion = 1;

        private readonly ISession session;
        private readonly IItemService items;
        private readonly GameConfig config;
        private readonly ISimulationService simulation;
        private readonly ILocationIndex locations;
        private readonly ISaveStore store;
        private readonly ISaveSerializer serializer;
        private readonly Func<DateTime> utcNow;
        private readonly List<Action> beforeSave = new();

        private Hero activeHero;
        private string activeName;

        /// <summary>Call sites at the Unity edge read the service as <c>HeroSaveService.Instance</c>.</summary>
        public static IHeroSaveService Instance => ServiceLocator.Get<IHeroSaveService>();

        /// <param name="simulation">Its Location registry resolves the Location ids a save holds, and its
        /// <see cref="ISimulationService.RunSettled"/> is a save point: a Recall and a Death each write the hero.
        /// The subscription is made once, here; the service is one instance, so a hero load or a World swap
        /// never needs it re-made.</param>
        /// <param name="utcNow">The clock for the saved-at stamp; the system clock when null.</param>
        public HeroSaveService(ISession session, IItemService items, GameConfig config, ISimulationService simulation,
            ISaveStore store, ISaveSerializer serializer, Func<DateTime> utcNow = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.items = items ?? throw new ArgumentNullException(nameof(items));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            locations = simulation.Locations;
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            this.utcNow = utcNow;

            simulation.RunSettled += _ => Save();
        }

        /// <summary>
        /// Registers a before-save normaliser from a view that may be enabled before a boot has armed the
        /// services (an enable in Edit Mode): does nothing, and says so, rather than throwing from the locator.
        /// </summary>
        /// <returns>Whether the normaliser was registered.</returns>
        public static bool TryAddBeforeSave(Action normaliser)
        {
            if (!ServiceLocator.IsArmed)
                return false;

            Instance.AddBeforeSave(normaliser);
            return true;
        }

        /// <summary>The matching removal for <see cref="TryAddBeforeSave"/>, tolerant of nothing being armed.</summary>
        public static void TryRemoveBeforeSave(Action normaliser)
        {
            if (ServiceLocator.IsArmed)
                Instance.RemoveBeforeSave(normaliser);
        }

        public string ActiveHeroId { get; private set; }

        public void AddBeforeSave(Action normaliser)
        {
            if (normaliser == null)
                throw new ArgumentNullException(nameof(normaliser));

            if (!beforeSave.Contains(normaliser))
                beforeSave.Add(normaliser);
        }

        public void RemoveBeforeSave(Action normaliser) => beforeSave.Remove(normaliser);

        public string LastSelectedHeroId
        {
            get
            {
                var result = AccountSlot().Load();
                return result.HasPayload && !string.IsNullOrEmpty(result.Payload.lastSelectedHeroId)
                    ? result.Payload.lastSelectedHeroId
                    : null;
            }
        }

        public IReadOnlyList<HeroSummary> List() =>
            store.Keys()
                .Where(key => key != AccountKey)
                .Select(SummaryOf)
                .OrderByDescending(hero => hero.SavedAtUtc)
                .ThenBy(hero => hero.Id, StringComparer.Ordinal)
                .ToArray();

        public HeroSummary Create(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A hero needs a name.", nameof(name));

            var id = Guid.NewGuid().ToString("N");

            // A throwaway hero, built the way a new one is, so the file holds exactly what a load restores.
            var hero = SessionBuilder.BuildHero(config, config.DefaultHero, items);
            HeroSlot(id).Save(HeroMapper.ToDto(hero, id, name.Trim(), locations));

            return SummaryOf(id);
        }

        public bool Delete(string id)
        {
            var deleted = store.Delete(Checked(id));

            if (LastSelectedHeroId == id)
                SetLastSelected(null);

            if (ActiveHeroId == id)
            {
                ActiveHeroId = null;
                activeHero = null;
            }

            return deleted;
        }

        public bool Rename(string id, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A hero needs a name.", nameof(name));

            var slot = HeroSlot(Checked(id));
            var result = slot.Load();

            // A file that cannot be read is left alone: saving over it would destroy what a repair might recover.
            if (!result.HasPayload)
                return false;

            result.Payload.name = name.Trim();
            slot.Save(result.Payload);

            if (ActiveHeroId == id)
                activeName = result.Payload.name;

            return true;
        }

        public void SetLastSelected(string id)
        {
            if (id != null && !store.TryRead(Checked(id), out _))
                throw new ArgumentException($"There is no saved hero '{id}'.", nameof(id));

            AccountSlot().Save(new AccountDto { lastSelectedHeroId = id ?? string.Empty });
        }

        public HeroLoadResult Load(string id)
        {
            var result = HeroSlot(Checked(id)).Load();

            if (!result.HasPayload)
            {
                NoteUnreadable(id, result);
                return new HeroLoadResult(result.Status, false, null);
            }

            if (!session.TryLoad(result.Payload, locations, out var report))
                return new HeroLoadResult(result.Status, false, null);

            ActiveHeroId = id;
            activeHero = session.Hero;
            activeName = result.Payload.name;
            SetLastSelected(id);

            if (result.Status == LoadStatus.RestoredFromBackup)
                Debug.LogWarning($"Hero save '{id}' was damaged; loaded its backup instead. The next save replaces the damaged file.");

            Quarantine(id, report);

            return new HeroLoadResult(result.Status, true, report);
        }

        public HeroLoadResult LoadLastOrCreate()
        {
            var candidates = new List<string>();

            var last = LastSelectedHeroId;
            if (last != null)
                candidates.Add(last);

            candidates.AddRange(List()
                .Where(hero => hero.Status is LoadStatus.Loaded or LoadStatus.RestoredFromBackup)
                .Select(hero => hero.Id)
                .Where(id => id != last));

            foreach (var id in candidates)
            {
                var result = Load(id);

                // Entered, or readable but refused (a Run is in the Field): either way, nothing to create.
                if (result.Status is LoadStatus.Loaded or LoadStatus.RestoredFromBackup)
                    return result;
            }

            return Load(Create(FirstHeroName).Id);
        }

        public bool Save()
        {
            // The Session's hero is the file's only while it is the one this service loaded; a template
            // load or another load behind its back leaves the file alone.
            if (ActiveHeroId == null || !ReferenceEquals(activeHero, session.Hero))
                return false;

            // A hero in the Field holds a bag the Run has not settled; the file only ever holds a Town hero.
            if (simulation.Run.Phase == RunPhase.InField)
                return false;

            RunNormalisers();

            // A write that fails must not take the game down. The write is atomic, so the previous save is
            // untouched; every save point writes the whole hero, so the next one is the retry.
            try
            {
                HeroSlot(ActiveHeroId).Save(HeroMapper.ToDto(session.Hero, ActiveHeroId, activeName, locations));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not save hero '{ActiveHeroId}': {exception.Message} The previous save is untouched; the next save point tries again.");
                return false;
            }
        }

        // Each runs on its own: one that throws must not keep the hero from being saved, and must not
        // keep the others from running.
        private void RunNormalisers()
        {
            foreach (var normaliser in beforeSave.ToArray())
            {
                try
                {
                    normaliser();
                }
                catch (Exception exception)
                {
                    Debug.LogError($"A before-save step failed and was skipped: {exception.Message}");
                }
            }
        }

        // A file that cannot become a hero. Corrupt (the backup was tried already) is set aside, never
        // deleted, so what is in it can still be recovered by hand. A newer version is left exactly as it
        // is: this build must not read it as a hero, and must not overwrite it.
        private void NoteUnreadable(string id, LoadResult<HeroDto> result)
        {
            switch (result.Status)
            {
                case LoadStatus.Corrupt:
                    _ = store.SetAside(id);

                    if (LastSelectedHeroId == id)
                        SetLastSelected(null);

                    Debug.LogError($"Hero save '{id}' is damaged and its backup is no use: {result.Failure} It was set aside, not deleted.");
                    break;

                case LoadStatus.NewerVersion:
                    Debug.LogWarning($"Hero save '{id}' was written by a newer version of the game. It is left untouched and not loaded.");
                    break;
            }
        }

        // What the load could not place goes to a sidecar beside the hero file, one JSON line each. The
        // game never reads it back, and a save never rewrites it. The items are already gone from the
        // loaded hero, so the next save will not carry them either.
        private void Quarantine(string id, RestoreReport report)
        {
            if (report.IsClean)
                return;

            store.AppendSideFile($"{id}.quarantine.json", QuarantineLog.Lines(report, utcNow?.Invoke() ?? DateTime.UtcNow));
            Debug.LogWarning($"Hero save '{id}': {report.Skipped.Count} saved item(s) could not be restored and were written to {id}.quarantine.json.");
        }

        private HeroSummary SummaryOf(string id)
        {
            var result = HeroSlot(id).Load();

            return result.HasPayload
                ? new HeroSummary(id, result.Payload.name, result.Payload.level, result.SavedAtUtc, result.Status)
                : new HeroSummary(id, string.Empty, 0u, result.SavedAtUtc, result.Status);
        }

        private ISaveSlot<HeroDto> HeroSlot(string id) =>
            new SaveSlot<HeroDto>(store, serializer, id, HeroSchemaVersion, utcNow: utcNow);

        private ISaveSlot<AccountDto> AccountSlot() =>
            new SaveSlot<AccountDto>(store, serializer, AccountKey, AccountSchemaVersion, utcNow: utcNow);

        private static string Checked(string id) =>
            string.IsNullOrEmpty(id) || id == AccountKey
                ? throw new ArgumentException($"'{id}' is not a hero id.", nameof(id))
                : id;
    }
}
