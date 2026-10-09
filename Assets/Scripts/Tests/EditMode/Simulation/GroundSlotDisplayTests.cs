using System;
using System.Reflection;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// A pooled entry of the floor views (epic #214, issue #221) is bound to one Drop at a time and goes back
    /// to the pool between uses: what the next Drop finds on it must be nothing of the last one. The entry lives
    /// in <c>Assembly-CSharp</c>, which a test assembly cannot reference, so it is reached by type name.
    /// </summary>
    [TestFixture]
    public sealed class GroundSlotDisplayTests
    {
        private const string TypeName = "ToolSmiths.InventorySystem.Runtime.Simulation.GroundItemSlotDisplay, Assembly-CSharp";
        private const string PotionId = "fake.potion";

        private GameObject _host;
        private Component _entry;
        private CanvasGroup _group;
        private Image _border;
        private ItemView _view;
        private ItemInstance _item;

        [SetUp]
        public void Build()
        {
            _host = new GameObject("entry");
            _group = _host.AddComponent<CanvasGroup>();
            _border = _host.AddComponent<Image>();
            _entry = _host.AddComponent(Type.GetType(TypeName, throwOnError: true));

            _entry.GetType().GetField("rarityBorder", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_entry, _border);

            _item = new ItemInstance(PotionId, ItemRarity.Rare, 1, null);
            var catalog = new InMemoryItemCatalog(new FakeItemDefinition { Id = PotionId, Category = ItemCategory.Consumable, BaseStackLimit = 10u });
            _view = ItemView.Resolve(_item, catalog);
        }

        [TearDown]
        public void Destroy() => UnityEngine.Object.DestroyImmediate(_host);

        private void Bind(float alpha, Action<ItemInstance> onClick = null)
        {
            Call("Bind", _item, 3u, _view, onClick);
            Call("Fade", alpha);
        }

        private object Call(string method, params object[] args) => _entry.GetType().GetMethod(method).Invoke(_entry, args);

        private object Read(string property) => _entry.GetType().GetProperty(property).GetValue(_entry);

        [Test]
        public void ABoundEntry_ShowsItsDrop_AtItsFade()
        {
            Bind(alpha: 0.5f);

            Assert.That(Read("Item"), Is.SameAs(_item));
            Assert.That(Read("Amount"), Is.EqualTo(3u));
            Assert.That(_group.alpha, Is.EqualTo(0.5f));
            Assert.That(_border.color, Is.EqualTo(_view.RarityColor));
        }

        [Test]
        public void AnUnboundEntry_CarriesNothingOfItsLastDrop()
        {
            Bind(alpha: 0.5f);

            Call("Unbind");

            Assert.That(Read("Item"), Is.Null);
            Assert.That(Read("Amount"), Is.EqualTo(0u));
            Assert.That(_group.alpha, Is.EqualTo(1f), "the next Drop starts opaque, not at the last one's fade");
            Assert.That(_border.color, Is.EqualTo(Color.white), "no rarity tint is left behind");
        }

        [Test]
        public void ARebindToTheSameDrop_AfterAnUnbind_IsDrawnAgain()
        {
            Bind(alpha: 0.5f);
            Call("Unbind");

            Bind(alpha: 1f);

            Assert.That(_border.color, Is.EqualTo(_view.RarityColor), "the early-out for an unchanged Drop must not skip a Drop that was let go");
            Assert.That(_group.alpha, Is.EqualTo(1f));
        }
    }
}
