using ToolSmiths.InventorySystem.Runtime.Character;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// What names the Session's hero on a screen: the name the player gave it, the class of its template and its
    /// level. Read off the save service and the hero, so every display of it (the Main Menu showcase, a Hero
    /// Panel title) says the same thing without each re-deriving it.
    /// </summary>
    public readonly struct HeroIdentity
    {
        public HeroIdentity(string name, string className, uint level)
        {
            Name = name;
            ClassName = className;
            Level = level;
        }

        /// <summary>No hero to name: the Session holds one nobody chose (the default template, or while an unreadable save is selected).</summary>
        public static HeroIdentity None => default;

        public string Name { get; }
        public string ClassName { get; }
        public uint Level { get; }

        public bool Exists => !string.IsNullOrEmpty(Name);

        /// <summary>
        /// The identity of <paramref name="hero"/> when it is the one the player has selected: a hero that came from
        /// a save or a draft, and not what the Session holds while an unreadable save is selected.
        /// </summary>
        public static HeroIdentity Of(IHeroSaveService saves, Hero hero) =>
            MainMenuFlow.CanPlay(saves) && saves.TryGet(saves.ActiveHeroId, out var summary)
                ? new HeroIdentity(summary.Name, summary.Template ? summary.Template.DisplayName : string.Empty, hero.Level)
                : None;
    }
}
