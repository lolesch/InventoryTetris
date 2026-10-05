using System.Collections.Generic;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Services;
using UnityEditor;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// One whole game as a fresh boot builds it - its own items, session and simulation service - plus
    /// the authored Locations a save refers to by id. For the persistence tests, which build a game,
    /// change it, save it, and load the save into a second one.
    /// </summary>
    internal sealed class TestGame
    {
        public ItemService Items { get; private set; }
        public Session Session { get; private set; }
        public SimulationService Simulation { get; private set; }

        public Hero Hero => Session.Hero;

        public static TestGame Create(GameConfig config)
        {
            var rolls = new SessionBuilderTests.FixedRolls(0.5f);
            var items = new ItemService(config, rolls);
            var session = SessionBuilder.Build(config, GameBoot.Load().DefaultHero, items);

            return new TestGame
            {
                Items = items,
                Session = session,
                Simulation = new SimulationService(session, items, new InventoryService(session, items), config, rolls),
            };
        }
    }

    internal static class TestLocations
    {
        /// <summary>A valid Location with this id, over the config's own loot distributions.</summary>
        public static LocationConfig Create(GameConfig config, List<Object> created, string id)
        {
            var location = ScriptableObject.CreateInstance<LocationConfig>();
            created.Add(location);

            var so = new SerializedObject(location);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("categoryDistribution").objectReferenceValue = config.ItemCategoryDistribution;
            so.FindProperty("rarityDistribution").objectReferenceValue = config.ItemRarityDistribution;
            _ = so.ApplyModifiedPropertiesWithoutUndo();

            return location;
        }

        /// <summary>Makes <paramref name="locations"/> the config's authored Locations.</summary>
        public static void Author(GameConfig config, params LocationConfig[] locations)
        {
            var so = new SerializedObject(config);
            var array = so.FindProperty("locations");
            array.arraySize = locations.Length;

            for (var i = 0; i < locations.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = locations[i];

            _ = so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
