using NUnit.Framework;
using ToolSmiths.InventorySystem.Runtime.Character;
using UnityEditor;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Character
{
    /// <summary>
    /// The authored side of issue #110: the scene's <c>LocalPlayer</c> is built from
    /// <c>DefaultHero.asset</c> now, so its prefab has to point at it. <c>LocalPlayer</c> lives in
    /// the predefined assembly, out of a test assembly's reach, so the component is read by name.
    /// </summary>
    [TestFixture]
    public sealed class AuthoredHeroAssetsTests
    {
        [Test]
        public void ThePlayerPrefab_BuildsItsLocalPlayerFromTheDefaultHeroData()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PLAYER.prefab");
            Assert.That(prefab, Is.Not.Null, "Assets/Prefabs/PLAYER.prefab is missing");

            var localPlayer = System.Array.Find(prefab.GetComponentsInChildren<MonoBehaviour>(true),
                component => component != null && component.GetType().Name == "LocalPlayer");
            Assert.That(localPlayer, Is.Not.Null, "no LocalPlayer on the prefab");

            var data = new SerializedObject(localPlayer).FindProperty("data").objectReferenceValue;

            Assert.That(data, Is.Not.Null, "LocalPlayer.data is unassigned");
            Assert.That(data, Is.InstanceOf<HeroData>());
            Assert.That(AssetDatabase.GetAssetPath(data), Is.EqualTo("Assets/Scripts/InventorySystem/Characters/DefaultHero.asset"));
        }
    }
}
