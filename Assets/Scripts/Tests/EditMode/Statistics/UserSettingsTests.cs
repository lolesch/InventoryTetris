using System.Collections.Generic;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Statistics
{
    public sealed class UserSettingsTests
    {
        private static readonly string[] Keys = { UserSettings.HoverDelayKey, UserSettings.GroundItemFadeDelayKey };

        private readonly Dictionary<string, float> saved = new();

        [SetUp]
        public void SetUp()
        {
            saved.Clear();

            foreach (var key in Keys)
            {
                if (PlayerPrefs.HasKey(key))
                    saved[key] = PlayerPrefs.GetFloat(key);

                PlayerPrefs.DeleteKey(key);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var key in Keys)
            {
                if (saved.TryGetValue(key, out var value))
                    PlayerPrefs.SetFloat(key, value);
                else
                    PlayerPrefs.DeleteKey(key);
            }
        }

        [Test]
        public void HoverDelay_UnsetReadsTheDefault() =>
            Assert.That(UserSettings.HoverDelay, Is.EqualTo(UserSettings.DefaultHoverDelay));

        [Test]
        public void HoverDelay_RoundTripsAValueInRange()
        {
            UserSettings.HoverDelay = 0.8f;

            Assert.That(UserSettings.HoverDelay, Is.EqualTo(0.8f));
        }

        [TestCase(-3f, UserSettings.MinHoverDelay)]
        [TestCase(99f, UserSettings.MaxHoverDelay)]
        public void HoverDelay_IsClampedToItsBounds(float written, float expected)
        {
            UserSettings.HoverDelay = written;

            Assert.That(UserSettings.HoverDelay, Is.EqualTo(expected));
        }

        [Test]
        public void GroundItemFadeDelay_UnsetReadsTheDefault() =>
            Assert.That(UserSettings.GroundItemFadeDelay, Is.EqualTo(UserSettings.DefaultGroundItemFadeDelay));

        [Test]
        public void GroundItemFadeDelay_RoundTripsAValueInRange()
        {
            UserSettings.GroundItemFadeDelay = 120f;

            Assert.That(UserSettings.GroundItemFadeDelay, Is.EqualTo(120f));
        }

        [TestCase(0f, UserSettings.MinGroundItemFadeDelay)]
        [TestCase(9999f, UserSettings.MaxGroundItemFadeDelay)]
        public void GroundItemFadeDelay_IsClampedToItsBounds(float written, float expected)
        {
            UserSettings.GroundItemFadeDelay = written;

            Assert.That(UserSettings.GroundItemFadeDelay, Is.EqualTo(expected));
        }

        [Test]
        public void ANaNWriteIsRefusedAndTheEarlierValueStays()
        {
            UserSettings.HoverDelay = 0.8f;
            UserSettings.HoverDelay = float.NaN;

            Assert.That(UserSettings.HoverDelay, Is.EqualTo(0.8f));
        }

        [Test]
        public void ANaNAlreadyStoredReadsTheDefault()
        {
            PlayerPrefs.SetFloat(UserSettings.GroundItemFadeDelayKey, float.NaN);

            Assert.That(UserSettings.GroundItemFadeDelay, Is.EqualTo(UserSettings.DefaultGroundItemFadeDelay));
        }
    }
}
