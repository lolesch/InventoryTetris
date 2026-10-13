using System;
using System.Reflection;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// A bar shows its resource as it stands, whichever way the resource moved. Gear changes the maximum while the
    /// sim is paused, when nothing regenerates the current value: the bar has to follow the maximum on its own.
    /// The display lives in <c>Assembly-CSharp</c>, which a test assembly cannot reference, so it is reached by type
    /// name; the fill is read off its mask, whose right padding is the empty part of a left-to-right bar.
    /// </summary>
    [TestFixture]
    public sealed class ResourceDisplayTests
    {
        private const string TypeName = "ToolSmiths.InventorySystem.GUI.Displays.ResourceDisplay, Assembly-CSharp";
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

        private GameObject _host;
        private RectMask2D _mask;
        private Component _display;
        private CharacterResource _health;

        [SetUp]
        public void Build()
        {
            _host = new GameObject("bar", typeof(RectTransform));
            _mask = new GameObject("mask", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectMask2D>();
            _mask.transform.SetParent(_host.transform, false);
            ((RectTransform)_mask.transform).sizeDelta = new Vector2(100f, 10f);

            _display = _host.AddComponent(Type.GetType(TypeName, throwOnError: true));
            Set("fillMask", _mask);
            Set("globeVolume", new AnimationCurve());

            _health = new CharacterResource(StatName.Health, 100f);
            _display.GetType().GetMethod("Bind").Invoke(_display, new object[] { _health, null });
        }

        [TearDown]
        public void Destroy() => UnityEngine.Object.DestroyImmediate(_host);

        private void Set(string field, object value) => _display.GetType().GetField(field, Fields).SetValue(_display, value);

        private static StatModifier Flat(float value) => new(new Vector2Int(0, 1000), value);

        // The empty part of the bar, as a share of its width.
        private float Empty => _mask.padding.z / 100f;

        [Test]
        public void ARaisedMaximum_ShrinksTheFill_WithoutTheCurrentValueMoving()
        {
            Assert.That(Empty, Is.EqualTo(0f), "a full bar");

            _health.AddModifier(Flat(100f));

            Assert.That(_health.CurrentValue, Is.EqualTo(100f), "the current value stayed where it was");
            Assert.That(Empty, Is.EqualTo(0.5f).Within(0.001f), "100 of 200");
        }

        [Test]
        public void ALoweredMaximum_StillClampsTheFill()
        {
            var bonus = Flat(100f);
            _health.AddModifier(bonus);
            _health.RefillCurrent();

            _ = _health.TryRemoveModifier(bonus);

            Assert.That(_health.CurrentValue, Is.EqualTo(100f));
            Assert.That(Empty, Is.EqualTo(0f), "a full bar again");
        }
    }
}
