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
    /// The <see cref="IHeroSaveService"/> over a save store: one slot per hero, keyed by its name and its
    /// generated id (<see cref="HeroFileKey"/>), and one for the Account. The store is injected, so the persistent data path is chosen
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
        private long activeCreatedAtTicks;

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

        public event Action HeroesChanged;

        // A view that throws must not undo or hide a change that is already made, so it is logged.
        private void RaiseHeroesChanged()
        {
            try
            {
                HeroesChanged?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogError($"A hero-list handler failed: {exception.Message}");
            }
        }

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
            KeysById()
                .Select(entry => SummaryOf(entry.Key, entry.Value))
                .OrderByDescending(hero => hero.SavedAtUtc)
                .ThenBy(hero => hero.Id, StringComparer.Ordinal)
                .ToArray();

        public HeroSummary Create(string name, string templateId = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A hero needs a name.", nameof(name));

            var template = string.IsNullOrEmpty(templateId) ? config.DefaultHero : config.FindHero(templateId);

            if (!string.IsNullOrEmpty(templateId) && template.Id != templateId)
                throw new ArgumentException($"There is no hero template '{templateId}'.", nameof(templateId));

            var id = Guid.NewGuid().ToString("N");

            // A throwaway hero, built the way a new one is, so the file holds exactly what a load restores.
            var hero = SessionBuilder.BuildHero(config, template, items);
            var dto = HeroMapper.ToDto(hero, id, name.Trim(), locations);
            dto.createdAtTicks = (utcNow?.Invoke() ?? DateTime.UtcNow).Ticks;

            var summary = SummaryOf(id, WriteHero(id, dto));

            RaiseHeroesChanged();
            return summary;
        }

        public bool Delete(string id)
        {
            var target = Checked(id);
            var deleted = false;

            // Every file of the id: a rename that was interrupted can leave two.
            foreach (var key in store.Keys().Where(key => key != AccountKey && HeroFileKey.IdOf(key) == target).ToArray())
                deleted |= store.Delete(key);

            if (LastSelectedHeroId == id)
                TrySetLastSelected(null);

            if (ActiveHeroId == id)
            {
                ActiveHeroId = null;
                activeHero = null;
            }

            RaiseHeroesChanged();
            return deleted;
        }

        public bool Rename(string id, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A hero needs a name.", nameof(name));

            var key = KeyOf(Checked(id));
            var result = key == null ? default : HeroSlot(key).Load();

            // A file that cannot be read is left alone: saving over it would destroy what a repair might recover.
            if (!result.HasPayload)
                return false;

            result.Payload.name = name.Trim();
            _ = WriteHero(id, result.Payload);

            if (ActiveHeroId == id)
                activeName = result.Payload.name;

            RaiseHeroesChanged();
            return true;
        }

        public void SetLastSelected(string id)
        {
            if (id != null && KeyOf(Checked(id)) == null)
                throw new ArgumentException($"There is no saved hero '{id}'.", nameof(id));

            AccountSlot().Save(new AccountDto { lastSelectedHeroId = id ?? string.Empty });
        }

        public HeroLoadResult Load(string id)
        {
            var key = KeyOf(Checked(id));

            if (key == null)
                return new HeroLoadResult(LoadStatus.Missing, false, null);

            var result = HeroSlot(key).Load();

            if (!result.HasPayload)
            {
                NoteUnreadable(id, key, result);

                // A corrupt file was set aside, so the list is not what it was.
                if (result.Status == LoadStatus.Corrupt)
                    RaiseHeroesChanged();

                return new HeroLoadResult(result.Status, false, null);
            }

            if (!session.TryLoad(result.Payload, locations, out var report))
                return new HeroLoadResult(result.Status, false, null);

            ActiveHeroId = id;
            activeHero = session.Hero;
            activeName = result.Payload.name;
            activeCreatedAtTicks = result.Payload.createdAtTicks;
            TrySetLastSelected(id);

            if (result.Status == LoadStatus.RestoredFromBackup)
                Debug.LogWarning($"Hero save '{id}' was damaged; loaded its backup instead. The next save replaces the damaged file.");

            // The file still holds what the load left out until it is written again, so a load that
            // quarantined something writes the hero at once: the next load must not append it a second time.
            // The write is skipped if the sidecar could not be written, so the items stay in the file.
            if (Quarantine(id, report))
                _ = Save();

            RaiseHeroesChanged();
            return new HeroLoadResult(result.Status, true, report);
        }

        public HeroLoadResult LoadLastOrCreate()
        {
            // The last-selected hero first; the common launch reads one file. Only when it cannot be
            // used are the other files listed, newest first.
            var last = LastSelectedHeroId;
            if (last != null && TryContinue(last, out var continued))
                return continued;

            foreach (var hero in List())
            {
                if (hero.Id != last
                    && hero.Status is LoadStatus.Loaded or LoadStatus.RestoredFromBackup
                    && TryContinue(hero.Id, out continued))
                    return continued;
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

            // A normaliser that failed left the hero in a state that is not safe to write (a held Package
            // that is in no container). The previous file still holds it, so keep that one.
            if (!RunNormalisers())
                return false;

            // A write that fails must not take the game down. The write is atomic, so the previous save is
            // untouched; every save point writes the whole hero, so the next one is the retry.
            try
            {
                var dto = HeroMapper.ToDto(session.Hero, ActiveHeroId, activeName, locations);
                dto.createdAtTicks = activeCreatedAtTicks;

                _ = WriteHero(ActiveHeroId, dto);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not save hero '{ActiveHeroId}': {exception.Message} The previous save is untouched; the next save point tries again.");
                return false;
            }
        }

        // Each runs on its own, so one that throws does not keep the others from running. A failure
        // vetoes this write, not the next: the previous file is intact and the next save point retries.
        private bool RunNormalisers()
        {
            var allRan = true;

            foreach (var normaliser in beforeSave.ToArray())
            {
                try
                {
                    normaliser();
                }
                catch (Exception exception)
                {
                    allRan = false;
                    Debug.LogError($"A before-save step failed, so this save was skipped and the previous one kept: {exception.Message}");
                }
            }

            return allRan;
        }

        // Entered, or readable but refused (a Run is in the Field): either way, nothing to create.
        private bool TryContinue(string id, out HeroLoadResult result)
        {
            result = Load(id);
            return result.Status is LoadStatus.Loaded or LoadStatus.RestoredFromBackup;
        }

        // The last-selected id is a convenience: failing to remember it must not undo a load or a delete.
        private void TrySetLastSelected(string id)
        {
            try
            {
                SetLastSelected(id);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not remember the last-selected hero: {exception.Message}");
            }
        }

        // A file that cannot become a hero. Corrupt (the backup was tried already) is set aside, never
        // deleted, so what is in it can still be recovered by hand. A newer version is left exactly as it
        // is: this build must not read it as a hero, and must not overwrite it.
        private void NoteUnreadable(string id, string key, LoadResult<HeroDto> result)
        {
            switch (result.Status)
            {
                case LoadStatus.Corrupt:
                    _ = store.SetAside(key);

                    if (LastSelectedHeroId == id)
                        TrySetLastSelected(null);

                    Debug.LogError($"Hero save '{id}' is damaged and its backup is no use: {result.Failure} It was set aside, not deleted.");
                    break;

                case LoadStatus.NewerVersion:
                    Debug.LogWarning($"Hero save '{id}' was written by a newer version of the game. It is left untouched and not loaded.");
                    break;
            }
        }

        // What the load could not place goes to a sidecar beside the hero file, one JSON line each. The
        // game never reads it back, and a save never rewrites it. The items are already gone from the
        // loaded hero, so the next save will not carry them either. Returns whether something was
        // quarantined, and so the file should be written again.
        private bool Quarantine(string id, RestoreReport report)
        {
            if (report.IsClean)
                return false;

            try
            {
                store.AppendSideFile($"{id}.quarantine.json", QuarantineLog.Lines(report, utcNow?.Invoke() ?? DateTime.UtcNow));
            }
            catch (Exception exception)
            {
                Debug.LogError($"Hero save '{id}': {report.Skipped.Count} saved item(s) could not be restored and could not be written to {id}.quarantine.json ({exception.Message}). The save file keeps them.");
                return false;
            }

            Debug.LogWarning($"Hero save '{id}': {report.Skipped.Count} saved item(s) could not be restored and were written to {id}.quarantine.json.");
            return true;
        }

        private HeroSummary SummaryOf(string id, string key)
        {
            var result = HeroSlot(key).Load();

            return result.HasPayload
                ? new HeroSummary(id, result.Payload.name, result.Payload.level, result.SavedAtUtc, result.Status,
                    config.FindHero(result.Payload.templateId), result.Payload.createdAtTicks)
                : new HeroSummary(id, string.Empty, 0u, result.SavedAtUtc, result.Status);
        }

        // Writes the hero under the key its name and id make, then removes any other file of the id: the
        // file it was under before a rename, or a bare-id file from before names were part of the key.
        // The new file is down first, so a failure or a crash in between leaves the hero twice, never
        // not at all; the newer of the two is the hero (KeysById), and the next write removes the other.
        private string WriteHero(string id, HeroDto dto)
        {
            var key = HeroFileKey.Compose(dto.name, id);
            var stale = store.Keys().Where(other => other != AccountKey && other != key && HeroFileKey.IdOf(other) == id).ToArray();

            HeroSlot(key).Save(dto);

            foreach (var other in stale)
            {
                try
                {
                    _ = store.Delete(other);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Hero save '{other}' is an older copy of '{key}' and could not be removed: {exception.Message}");
                }
            }

            return key;
        }

        // The file of the hero: the key a listing finds for its id, which a rename or an older build may have
        // named differently from what the name now makes. null when there is none.
        private string KeyOf(string id) => KeysById().TryGetValue(id, out var key) ? key : null;

        // Every hero file by the id it carries. Two files of one id (an interrupted rename) are one hero: the
        // one saved last, and the other is left for the next write to remove.
        private Dictionary<string, string> KeysById()
        {
            var byId = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var key in store.Keys())
            {
                if (key == AccountKey)
                    continue;

                var id = HeroFileKey.IdOf(key);
                byId[id] = byId.TryGetValue(id, out var other) ? Newer(other, key) : key;
            }

            return byId;
        }

        private string Newer(string first, string second)
        {
            var firstAt = HeroSlot(first).Load().SavedAtUtc;
            var secondAt = HeroSlot(second).Load().SavedAtUtc;

            return firstAt != secondAt
                ? firstAt > secondAt ? first : second
                : string.CompareOrdinal(first, second) <= 0 ? first : second;
        }

        private ISaveSlot<HeroDto> HeroSlot(string key) =>
            new SaveSlot<HeroDto>(store, serializer, key, HeroSchemaVersion, utcNow: utcNow);

        private ISaveSlot<AccountDto> AccountSlot() =>
            new SaveSlot<AccountDto>(store, serializer, AccountKey, AccountSchemaVersion, utcNow: utcNow);

        private static string Checked(string id) =>
            string.IsNullOrEmpty(id) || id == AccountKey
                ? throw new ArgumentException($"'{id}' is not a hero id.", nameof(id))
                : id;
    }
}
