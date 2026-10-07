using System.Linq;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>What a view needs of <see cref="IHeroSaveService"/> that is a composition of its calls, so the
    /// hero slots and the dev tools do not each re-state it.</summary>
    public static class HeroSaveServiceExtensions
    {
        private const string NamePrefix = "Hero ";

        /// <summary>
        /// Creates a hero from the default template under <see cref="NextName"/> and loads it. The hero is
        /// written first, so a load that is refused (a Run is in the Field) still leaves it in the list.
        /// </summary>
        public static HeroLoadResult CreateAndLoad(this IHeroSaveService saves) =>
            saves.Load(saves.Create(NextName(saves)).Id);

        /// <summary>"Hero N" for the smallest N no saved hero is called, so deleting one and creating another
        /// never gives two heroes the same name.</summary>
        public static string NextName(this IHeroSaveService saves)
        {
            var taken = saves.List().Select(hero => hero.Name).ToHashSet();

            for (var number = 1; ; number++)
            {
                var name = NamePrefix + number;

                if (!taken.Contains(name))
                    return name;
            }
        }
    }
}
