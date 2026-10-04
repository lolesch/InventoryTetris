using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    /// <summary>
    /// The template a <see cref="Hero"/> is built from: its base stats, base resources and
    /// starting level. An authored asset for a new hero (a DTO takes its place for a loaded one);
    /// <c>DefaultHero.asset</c> reproduces what the scene's <c>LocalPlayer</c> used to serialize.
    /// Read-only by construction - the <see cref="Hero"/> copies the values into stats of its own,
    /// because with domain reload disabled a runtime write to a <see cref="ScriptableObject"/>
    /// survives Stop.
    /// </summary>
    [CreateAssetMenu(menuName = "InventorySystem/Hero/Hero Data")]
    public sealed class HeroData : ScriptableObject
    {
        /// <summary>One stat or resource and the value it starts from, before any modifier.</summary>
        [Serializable]
        public struct BaseStat
        {
            [field: SerializeField] public StatName Stat { get; private set; }
            [field: SerializeField] public float BaseValue { get; private set; }

            public BaseStat(StatName stat, float baseValue)
            {
                Stat = stat;
                BaseValue = baseValue;
            }
        }

        [SerializeField] private BaseStat[] stats = Array.Empty<BaseStat>();
        [SerializeField] private BaseStat[] resources = Array.Empty<BaseStat>();

        [field: SerializeField, Range(1, 100)] public uint Level { get; private set; } = 1;

        /// <summary>Every stat that is not a resource.</summary>
        public IReadOnlyList<BaseStat> Stats => Array.AsReadOnly(stats);

        /// <summary>Health, Resource, Shield and Experience: the stats that also track a current value.</summary>
        public IReadOnlyList<BaseStat> Resources => Array.AsReadOnly(resources);
    }
}
