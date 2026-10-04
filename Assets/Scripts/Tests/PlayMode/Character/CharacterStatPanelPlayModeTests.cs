using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Character
{
    /// <summary>
    /// The stat sheet's life cycle (issue #111), on the real <c>PLAYER</c> prefab. <c>CharacterStatPanel</c>
    /// and <c>LocalPlayer</c> live in the predefined assembly, out of a test assembly's reach, so they are
    /// read by name; the boot's <c>ItemService</c> is what draws the rows' icons, so this needs Play Mode.
    /// Run with the PlayMode filter; <c>Run All</c> in the EditMode tab does not include it.
    /// </summary>
    public sealed class CharacterStatPanelPlayModeTests
    {
        private GameObject _player;
        private MonoBehaviour _panel;
        private Hero _hero;

        private static readonly CharacterStatModifier[] Gear =
        {
            new(StatName.Armor, new StatModifier(new Vector2Int(0, 1000), 10f)),
        };

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PLAYER.prefab");
            _player = Object.Instantiate(prefab);
#else
            Assert.Ignore("the PLAYER prefab is loaded by path, which only the Editor can do");
#endif

            var localPlayer = Named("LocalPlayer");
            _panel = Named("CharacterStatPanel");
            _hero = (Hero)localPlayer.GetType().GetProperty("Hero").GetValue(localPlayer);

            yield return null;
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_player);

        private MonoBehaviour Named(string typeName) =>
            System.Array.Find(_player.GetComponentsInChildren<MonoBehaviour>(true), c => c != null && c.GetType().Name == typeName);

        private MonoBehaviour[] RowComponents(bool includeInactive) =>
            _panel.GetComponentsInChildren<MonoBehaviour>(includeInactive).Where(c => c != null && c.GetType().Name == "CharacterStatDisplay").ToArray();

        private string Sheet() =>
            string.Join("\n", RowComponents(false).Select(row => row.GetComponentInChildren<TextMeshProUGUI>(true).text));

        private int Rows(bool includeInactive) => RowComponents(includeInactive).Length;

        [UnityTest]
        public IEnumerator TheSheet_FollowsTheHero_AndLetsGoOfItWhileDisabled()
        {
            var before = Sheet();
            Assert.That(Rows(false), Is.EqualTo(_hero.Resources.Count + _hero.Stats.Count), "one row per stat and resource");

            _hero.AddItemStats(Gear);
            yield return null;
            var worn = Sheet();
            Assert.That(worn, Is.Not.EqualTo(before), "equipping updates the sheet");

            _hero.RemoveItemStats(Gear);
            yield return null;
            Assert.That(Sheet(), Is.EqualTo(before), "unequipping updates it back");

            _panel.enabled = false;
            _hero.AddItemStats(Gear);
            yield return null;
            Assert.That(Sheet(), Is.EqualTo(before), "a disabled panel is not listening");

            _panel.enabled = true;
            Assert.That(Sheet(), Is.EqualTo(worn), "enabling rebuilds from the hero as it is now");

            // The second Play entry of a session: disable, enable, again.
            _panel.enabled = false;
            _panel.enabled = true;
            _hero.RemoveItemStats(Gear);
            yield return null;
            Assert.That(Sheet(), Is.EqualTo(before), "re-enabling subscribed again, once");
        }

        [UnityTest]
        public IEnumerator TheSheet_ReplacesRowsAPreviousSessionDestroyed_InsteadOfAddingBeside()
        {
            var made = Rows(true);

            // What a Play exit does to runtime-made rows while the panel itself survives.
            foreach (var row in RowComponents(false))
                Object.Destroy(row.gameObject);

            yield return null;
            _panel.enabled = false;
            _panel.enabled = true;

            Assert.That(Rows(false), Is.EqualTo(_hero.Resources.Count + _hero.Stats.Count));
            Assert.That(Rows(true), Is.EqualTo(made), "the lost rows are made again, not made on top of a growing list");
        }
    }
}
