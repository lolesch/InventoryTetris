using Submodules.Utility.Persistence;
using Submodules.Utility.Services;
using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Persistence;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>One saved hero as the hero list shows it. A hero whose file cannot be read has no name and level 0.</summary>
    public readonly struct HeroSummary
    {
        public HeroSummary(string id, string name, uint level, DateTime savedAtUtc, LoadStatus status)
        {
            Id = id;
            Name = name;
            Level = level;
            SavedAtUtc = savedAtUtc;
            Status = status;
        }

        /// <summary>The generated id the file is named by. Never changes, so a rename never moves the file.</summary>
        public string Id { get; }

        /// <summary>Display only.</summary>
        public string Name { get; }

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

    /// <summary>
    /// The saved heroes and the Account (GLOSSARY.md "Account"): which heroes exist, which one was
    /// last used, and moving one into and out of the Session. One file per hero, named by its
    /// generated id; the hero list is the files themselves, so there is no index to fall out of step.
    /// </summary>
    public interface IHeroSaveService : IService
    {
        /// <summary>Every saved hero, newest save first.</summary>
        IReadOnlyList<HeroSummary> List();

        /// <summary>Writes a new hero called <paramref name="name"/>, built from the default template, under a new id. Does not load it.</summary>
        HeroSummary Create(string name);

        /// <summary>Removes the hero's file and its backup. <c>false</c> when there was none.</summary>
        bool Delete(string id);

        /// <summary>Changes the display name. The file keeps its name. <c>false</c> when the hero cannot be read.</summary>
        bool Rename(string id, string name);

        /// <summary>The hero last loaded or chosen, or <c>null</c> when there is none.</summary>
        string LastSelectedHeroId { get; }

        /// <summary>Makes <paramref name="id"/> (or none, for <c>null</c>) the last-selected hero. Throws for an id with no file.</summary>
        void SetLastSelected(string id);

        /// <summary>
        /// Loads the hero into the Session and makes it the last-selected one. Refused, with
        /// <see cref="HeroLoadResult.Entered"/> false, while a Run is in the Field.
        /// </summary>
        HeroLoadResult Load(string id);

        /// <summary>
        /// The hero a launch continues: the last-selected one, else the newest that reads, else a new
        /// hero called "Hero", written at once so leaving the game in Town without a Run still leaves a save.
        /// </summary>
        HeroLoadResult LoadLastOrCreate();

        /// <summary>The id of the hero the Session holds, when it came from <see cref="Load"/>; else <c>null</c>.</summary>
        string ActiveHeroId { get; }

        /// <summary>
        /// Writes the Session's hero over its own file. <c>false</c> when the Session's hero did not come
        /// from a save (a template load, or another hero loaded behind this service's back), so a
        /// hero that is not the file's can never overwrite it.
        /// </summary>
        bool Save();

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
