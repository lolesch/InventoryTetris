using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Character
{
    /// <summary>
    /// The authored side of issues #110, #112 and #119: the boot builds the hero from
    /// <c>DefaultHero.asset</c> (<c>GameConfig.DefaultHero</c>), so no component holds a hero or a
    /// template of its own, and the <c>PLAYER</c> prefab's stat sheet reads the Session's hero.
    /// <c>CharacterStatSheet</c> lives in the predefined assembly, out of a test assembly's reach, so
    /// the component is read by name.
    /// </summary>
    [TestFixture]
    public sealed class AuthoredHeroAssetsTests
    {
        private static GameObject PlayerPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PLAYER.prefab");
            Assert.That(prefab, Is.Not.Null, "Assets/Prefabs/PLAYER.prefab is missing");
            return prefab;
        }

        private static MonoBehaviour ComponentNamed(GameObject prefab, string typeName) =>
            System.Array.Find(prefab.GetComponentsInChildren<MonoBehaviour>(true),
                component => component != null && component.GetType().Name == typeName);

        [Test]
        public void ThePlayerPrefab_HoldsNoHeroComponent_TheBootBuildsTheHero()
        {
            // A leftover component of a deleted script is a missing script: the hero has no scene face.
            foreach (var child in PlayerPrefab().GetComponentsInChildren<Transform>(true))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject), Is.Zero,
                    $"{child.name} holds a component whose script is gone");
        }

        [Test]
        public void ThePlayerPrefab_ShowsTheSessionsHeroOnACharacterStatSheet()
        {
            var sheet = ComponentNamed(PlayerPrefab(), "CharacterStatSheet");
            Assert.That(sheet, Is.Not.Null, "no CharacterStatSheet on the prefab");

            var serialized = new SerializedObject(sheet);

            Assert.That(serialized.FindProperty("character"), Is.Null, "the sheet reads the hero off the Session, not a character");
            Assert.That(serialized.FindProperty("rowPrefab").objectReferenceValue, Is.Not.Null,
                "the sheet has no row to make its rows from");
        }
    }
}
