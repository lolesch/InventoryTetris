using Submodules.Utility.Persistence;
using Submodules.Utility.Services;
using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Persistence;
using ToolSmiths.InventorySystem.Runtime.Character;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>One saved hero as the hero list shows it. A hero whose file cannot be read has no name, no template and level 0.</summary>
    public readonly struct HeroSummary
    {
        /// <summary>Shown for a file that is damaged and has no usable backup.</summary>
        public const string CorruptReason = "File damaged and no backup could be read";

        /// <summary>Shown for a file written by a newer build.</summary>
        public const string NewerVersionReason = "Saved by a newer version of the game";

        /// <summary>Shown for a file that read but would not become a hero when it was chosen.</summary>
        public const string NotLoadableReason = "This save could not be loaded";

        /// <param name="loadFailure">Why a file that reads would not load, or <c>null</c>.</param>
        public HeroSummary(string id, string name, uint level, DateTime savedAtUtc, LoadStatus status, HeroData template = null,
            long createdAtTicks = 0L, string loadFailure = null)
        {
            Id = id;
            Name = name;
            Level = level;
            SavedAtUtc = savedAtUtc;
            Status = status;
            Template = template;
            CreatedAtTicks = createdAtTicks;
            UnreadableReason = status switch
            {
                LoadStatus.Corrupt => CorruptReason,
                LoadStatus.NewerVersion => NewerVersionReason,
                _ => loadFailure,
            };
        }

        /// <summary>Why the hero cannot be entered, in words for the player; <c>null</c> for a hero that loads. The file
        /// is not read as a hero in any of these cases, and the only thing left to do with it is delete it.</summary>
        public string UnreadableReason { get; }

        /// <summary>When the hero was created, in UTC ticks; 0 for a save written before there was a stamp
        /// and for a file that cannot be read. Unlike <see cref="SavedAtUtc"/> a save does not move it, so
        /// ordering by it keeps each hero where it was.</summary>
        public long CreatedAtTicks { get; }

        /// <summary>The generated id the file is named by (after the name). Never changes, so a rename keeps the identity.</summary>
        public string Id { get; }

        /// <summary>The hero's own name, as the player typed it. A rename changes it and moves the file to the new name.</summary>
        public string Name { get; }

        /// <summary>The template the hero is built from: its <see cref="HeroData.Icon"/> and class name. <c>null</c> for an unreadable file.</summary>
        public HeroData Template { get; }

        public uint Level { get; }
        public DateTime SavedAtUtc { get; }

        /// <summary>How the file read: <see cref="LoadStatus.Loaded"/>, or the reason it did not.</summary>
        public LoadStatus Status { get; }
    }

    /// <summary>What a <see cref="IHeroSaveService.Load"/> did.</summary>
    public readonly struct HeroLoadResult
    {
        public HeroLoadResult(LoadStatus status, bool entered, RestoreReport report)
        {
            Status = status;
            Entered = entered;
            Report = report;
        }

        /// <summary>How the file read.</summary>
        public LoadStatus Status { get; }

        /// <summary>Whether the hero is now the Session's. <c>false</c> for a file that could not be read, and for a
        /// readable one the Session refused because a Run is in the Field.</summary>
        public bool Entered { get; }

        /// <summary>What the restore could not place; <c>null</c> unless <see cref="Entered"/>.</summary>
        public RestoreReport Report { get; }
    }

    /// <summary>Why a <see cref="IHeroSaveService.Save"/> did not write.</summary>
    public enum SaveFailure
    {
        /// <summary>It wrote.</summary>
        None,

        /// <summary>The Session's hero did not come from a save file (a template load, or another hero loaded behind the service's back).</summary>
        NotFromSave,

        /// <summary>A Run is in the Field; the file only ever holds a Town hero.</summary>
        RunInField,

        /// <summary>A before-save step failed, so the previous file was kept.</summary>
        BeforeSaveFailed,

        /// <summary>The store could not write the file; the previous one is untouched.</summary>
        StoreFailed,
    }

    /// <summary>
    /// What a <see cref="IHeroSaveService.Save"/> did: whether it wrote and, when it did not, a short
    /// player-facing <see cref="Reason"/>. The technical text of a failure stays in the log.
    /// </summary>
    public readonly struct SaveResult
    {
        private SaveResult(SaveFailure failure, string reason)
        {
            Failure = failure;
            Reason = reason;
        }

        /// <summary>The hero is written.</summary>
        public static SaveResult Written => new(SaveFailure.None, string.Empty);

        public static SaveResult Failed(SaveFailure failure) => new(failure, failure switch
        {
            SaveFailure.NotFromSave => "This hero did not come from a save file, so there is nothing to write.",
            SaveFailure.RunInField => "A Run is in the Field, and a hero is only saved in Town.",
            SaveFailure.BeforeSaveFailed => "Something the game does before saving failed, so the previous save was kept.",
            SaveFailure.StoreFailed => "The save could not be written: storage is blocked or has no room.",
            _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, "A failure is not None."),
        });

        public SaveFailure Failure { get; }

        /// <summary>Whether the hero was written.</summary>
        public bool Wrote => Failure == SaveFailure.None;

        /// <summary>A short plain-language cause; empty when the hero was written.</summary>
        public string Reason { get; }
    }

    /// <summary>
    /// The saved heroes and the Account (GLOSSARY.md "Account"): which heroes exist, which one was
    /// last used, and moving one into and out of the Session. One file per hero, named by its
    /// generated id and its name (<see cref="HeroFileKey"/>); the hero list is the files themselves, so
    /// there is no index to fall out of step.
    /// </summary>
    public interface IHeroSaveService : IService
    {
        /// <summary>Every saved hero, newest save first.</summary>
        IReadOnlyList<HeroSummary> List();

        /// <summary>
        /// Raised after the hero list or the active hero changed: a hero was created, deleted, renamed or
        /// loaded, the last-selected hero was chosen, a hero would not load, or an unreadable file was set aside. A view of the list refreshes from it instead of
        /// every caller that changes a hero having to know the view exists. A handler that throws is logged
        /// and does not undo the change.
        /// </summary>
        event Action HeroesChanged;

        /// <summary>
        /// Writes a new hero called <paramref name="name"/> under a new id, built from the template
        /// <paramref name="templateId"/> (<see cref="HeroData.Id"/>), or from the default template when
        /// it is <c>null</c>. Throws for an id no template has. Does not load it.
        /// </summary>
        HeroSummary Create(string name, string templateId = null);

        /// <summary>Removes the hero's file and its backup. <c>false</c> when there was none.</summary>
        bool Delete(string id);

        /// <summary>Changes the name, and the file's name with it; the id stays. <c>false</c> when the hero cannot be read.</summary>
        bool Rename(string id, string name);

        /// <summary>One hero as <see cref="List"/> shows it, reading only its file. <c>false</c> for an id with no file.</summary>
        bool TryGet(string id, out HeroSummary hero);

        /// <summary>The hero last loaded or chosen, or <c>null</c> when there is none.</summary>
        string LastSelectedHeroId { get; }

        /// <summary>
        /// Makes <paramref name="id"/> (or none, for <c>null</c>) the last-selected hero and raises
        /// <see cref="HeroesChanged"/>. Throws for an id with no file. The id need not be one that reads: the
        /// Main Menu selects an unreadable hero this way so it can be shown and deleted. That selection lasts
        /// this session only: a launch must not find a damaged file named as the hero to continue, since loading
        /// it sets the file aside.
        /// </summary>
        void SetLastSelected(string id);

        /// <summary>
        /// Loads the hero into the Session and makes it the last-selected one. Refused, with
        /// <see cref="HeroLoadResult.Entered"/> false, while a Run is in the Field.
        /// </summary>
        HeroLoadResult Load(string id);

        /// <summary>
        /// What the game scene tells the player on entering the active hero, once: the notice its load made (a
        /// backup restore, packages that were left out), or <c>null</c> after a clean load or when it was taken
        /// already. Held across scene loads until taken; a notice for a hero that is not active waits for it.
        /// </summary>
        EntryNotice TakeEntryNotice();

        /// <summary>
        /// The hero a launch continues: the last-selected one, else the newest that reads. Creates nothing:
        /// with no hero that reads the result is <see cref="LoadStatus.Missing"/> and not entered, and the
        /// Session keeps the hero it had.
        /// </summary>
        HeroLoadResult LoadLast();

        /// <summary>
        /// <see cref="LoadLast"/>, else a new hero called "Hero", written at once so leaving the game in
        /// Town without a Run still leaves a save. The Editor's launch; a built player uses <see cref="LoadLast"/>.
        /// </summary>
        HeroLoadResult LoadLastOrCreate();

        /// <summary>The id of the hero the Session holds, when it came from <see cref="Load"/>; else <c>null</c>.</summary>
        string ActiveHeroId { get; }

        /// <summary>
        /// Writes the Session's hero over its own file. Refused, with <see cref="SaveFailure.NotFromSave"/>,
        /// when the Session's hero did not come from a save (a template load, or another hero loaded behind
        /// this service's back), so a hero that is not the file's can never overwrite it. A failure says why
        /// in <see cref="SaveResult.Reason"/>; the next save point is the retry.
        /// </summary>
        SaveResult Save();

        /// <summary>
        /// Registers a normaliser that runs at the start of every <see cref="Save"/> that is going to
        /// write, before the hero is read. It exists for state the engine-free save code cannot see: a
        /// Package held on the cursor belongs to no container, so the drag provider registers one that
        /// returns it to its origin. Normalisers run in registration order. One that throws is logged,
        /// the others still run, and this save is skipped: the previous file is intact and the next save
        /// point retries. Registering the same one twice keeps one.
        /// </summary>
        void AddBeforeSave(System.Action normaliser);

        /// <summary>Removes a normaliser. Does nothing for one that is not registered.</summary>
        void RemoveBeforeSave(System.Action normaliser);
    }
}
