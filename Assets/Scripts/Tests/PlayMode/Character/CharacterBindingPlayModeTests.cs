using System.Collections;
using NUnit.Framework;
using Submodules.Utility.Services;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Services;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Character
{
    /// <summary>
    /// A scene face follows the hero of the Session it finds when it is enabled (issue #112). With
    /// scene reload disabled the <c>LocalPlayer</c> outlives a Stop and the next Play entry hands it
    /// a freshly booted hero: <c>Awake</c> and <c>Start</c> do not run again, <c>OnEnable</c> does.
    /// <c>LocalPlayer</c> lives in the predefined assembly, so it is read by name. Run with the
    /// PlayMode filter.
    /// </summary>
    public sealed class CharacterBindingPlayModeTests
    {
        private GameObject _player;
        private MonoBehaviour _localPlayer;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            _player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PLAYER.prefab"));
#else
            Assert.Ignore("the PLAYER prefab is loaded by path, which only the Editor can do");
#endif
            _localPlayer = System.Array.Find(_player.GetComponentsInChildren<MonoBehaviour>(true),
                c => c != null && c.GetType().Name == "LocalPlayer");

            yield return null;
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_player);

        private Hero HeroOf(MonoBehaviour face) => (Hero)face.GetType().GetProperty("Hero").GetValue(face);

        [UnityTest]
        public IEnumerator AReEnabledFace_ReactsToTheNewHero_AndNoLongerToTheOldOne()
        {
            var old = HeroOf(_localPlayer);

            // The next Play entry: the boot builds a new Session, then the surviving face is enabled again.
            _localPlayer.enabled = false;
            ServiceLocator.Reset();
            GameBoot.Arm(GameBoot.Load());
            _localPlayer.enabled = true;

            var current = HeroOf(_localPlayer);
            Assert.That(current, Is.Not.SameAs(old), "the boot built a new hero");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("DIED!"));
            current.GetResource(StatName.Health).DepleteCurrent();
            yield return null;

            old.GetResource(StatName.Health).DepleteCurrent();
            yield return null;

            LogAssert.NoUnexpectedReceived();
        }
    }
}
