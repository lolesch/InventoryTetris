using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.TestTools;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Services
{
    /// <summary>
    /// The <c>DebugPanel</c> controls (issue #118) and its dev log of the Hero's death. The panel lives in the
    /// predefined assembly, out of a test assembly's reach, so it is added and called by name. Needs Play Mode:
    /// the boot's Session holds the Hero and World the controls act on. Run with the PlayMode filter.
    /// </summary>
    public sealed class DebugPanelPlayModeTests
    {
        private GameObject _host;
        private Component _panel;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var type = System.Type.GetType("ToolSmiths.InventorySystem.GUI.Components.Panels.DebugPanel, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, "DebugPanel is in the predefined assembly");

            _host = new GameObject("DebugPanelTestHost");
            _panel = _host.AddComponent(type);

            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_host);

            // The Session outlives the test with domain reload off: hand the next test a fresh Hero.
            _ = Session.Instance.TryLoad(GameBoot.Load().DefaultHero);
        }

        private void Call(string method) => _panel.GetType().GetMethod(method).Invoke(_panel, null);

        [Test]
        public void StashInventory_MovesWhatTheStashTook_OutOfTheInventory()
        {
            var hero = Session.Instance.Hero;
            var package = new Package(null, ItemService.Instance.RollEquipment(EquipmentType.Helm, 0f), 1u);

            Assert.That(hero.Inventory.TryAddToContainer(ref package), Is.True, "the helm fits the Inventory");
            Assert.That(hero.Inventory.StoredPackages.Count, Is.EqualTo(1));

            Call("StashInventory");

            Assert.That(hero.Inventory.StoredPackages.Count, Is.Zero, "the Inventory gave it up");
            Assert.That(hero.Stash.StoredPackages.Count, Is.EqualTo(1), "the Stash took it");
        }

        [Test]
        public void ClearHealerSupply_EmptiesTheShelf_ClearVendorSupplyTheOther()
        {
            var world = Session.Instance.World;

            Call("RestockStore");
            Call("RestockHealerSupply");
            Assert.That(world.VendorSupply.StoredPackages.Count, Is.Positive);
            Assert.That(world.HealerSupply.StoredPackages.Count, Is.Positive);

            Call("ClearHealerSupply");
            Assert.That(world.HealerSupply.StoredPackages.Count, Is.Zero);
            Assert.That(world.VendorSupply.StoredPackages.Count, Is.Positive, "the Vendor's shelf is not the Healer's");

            Call("ClearVendorSupply");
            Assert.That(world.VendorSupply.StoredPackages.Count, Is.Zero);
        }

        [Test]
        public void KillPlayer_LogsTheDeath_AndStillDoesAfterAHeroLoad()
        {
            LogAssert.Expect(LogType.Warning, new Regex("DIED!"));
            Call("KillPlayer");

            LogAssert.Expect(LogType.Log, "Loaded a new Hero and World from the default hero.");
            Call("LoadDefaultHero");
            Assert.That(Session.Instance.Hero.IsDead, Is.False, "the loaded Hero is a fresh one");

            LogAssert.Expect(LogType.Warning, new Regex("DIED!"));
            Call("KillPlayer");
        }
    }
}
