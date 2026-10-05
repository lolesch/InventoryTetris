using NUnit.Framework;
using ToolSmiths.InventorySystem.Runtime.Character;
using UnityEditor;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Character
{
    /// <summary>
    /// The authored side of issues #110 and #112: the scene's <c>LocalPlayer</c> wraps the hero the
    /// boot built from <c>DefaultHero.asset</c> (<c>GameConfig.DefaultHero</c>), so its prefab holds
    /// no template of its own. <c>LocalPlayer</c> lives in the predefined assembly, out of a test
    /// assembly's reach, so the component is read by name.
    /// </summary>
    [TestFixture]
    public sealed class AuthoredHeroAssetsTests
    {
        [Test]
        public void ThePlayerPrefab_HoldsNoHeroTemplate_TheBootBuildsTheHero()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PLAYER.prefab");
            Assert.That(prefab, Is.Not.Null, "Assets/Prefabs/PLAYER.prefab is missing");

            var localPlayer = System.Array.Find(prefab.GetComponentsInChildren<MonoBehaviour>(true),
                component => component != null && component.GetType().Name == "LocalPlayer");
            Assert.That(localPlayer, Is.Not.Null, "no LocalPlayer on the prefab");

            Assert.That(new SerializedObject(localPlayer).FindProperty("data"), Is.Null,
                "a template on the component would be a second hero that the containers do not know");
        }

        private static MonoBehaviour ComponentNamed(GameObject prefab, string typeName) =>
            System.Array.Find(prefab.GetComponentsInChildren<MonoBehaviour>(true),
                component => component != null && component.GetType().Name == typeName);

        [Test]
        public void ThePlayerPrefab_ShowsItsHeroOnACharacterStatPanel()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PLAYER.prefab");
            var localPlayer = ComponentNamed(prefab, "LocalPlayer");
            var panel = ComponentNamed(prefab, "CharacterStatPanel");
            Assert.That(panel, Is.Not.Null, "no CharacterStatPanel on the prefab");

            var serialized = new SerializedObject(panel);

            Assert.That(serialized.FindProperty("character").objectReferenceValue, Is.SameAs(localPlayer),
                "the panel reads the hero off the player's character");
            Assert.That(serialized.FindProperty("rowPrefab").objectReferenceValue, Is.Not.Null,
                "the panel has no row to make its rows from");
        }

        [Test]
        public void TheLocalPlayer_HoldsNoStatDisplay()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PLAYER.prefab");
            var localPlayer = new SerializedObject(ComponentNamed(prefab, "LocalPlayer"));

            Assert.That(localPlayer.FindProperty("characterStatPrefab"), Is.Null);
            Assert.That(localPlayer.FindProperty("characterStatPool"), Is.Null);
        }
    }
}
