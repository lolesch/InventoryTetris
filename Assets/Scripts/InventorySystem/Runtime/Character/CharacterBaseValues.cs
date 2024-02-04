using System;
using System.Linq;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "New CharacterBaseValues", menuName = "Inventory System/Character/BaseValues")]
    public class CharacterBaseValues : ScriptableObject
    {
        [field: SerializeField] public CharacterStatBaseValue[] CharacterStats { get; protected set; } = new CharacterStatBaseValue[0];
        [field: SerializeField] public CharacterStatBaseValue[] CharacterResources { get; protected set; } = new CharacterStatBaseValue[0];

        // TODO: derive from monsterLevel and combat rating?
        [field: SerializeField] public uint ExperienceWhenKilled { get; protected set; }

        private void Awake() => ResetStatsAndResources();

        [ContextMenu("ResetStatsAndResources")]
        private void ResetStatsAndResources()
        {
            var resourcesOnly = new StatName[] { StatName.Health, StatName.Resource, StatName.Shield, StatName.Experience };

            var statNames = Enum.GetValues(typeof(StatName)) as StatName[];
            var statsOnly = statNames.ToList();

            foreach (var resource in resourcesOnly)
                statsOnly.Remove(resource);

            if (CharacterStats.Length != statsOnly.Count)
            {
                var previous = CharacterStats;
                CharacterStats = new CharacterStatBaseValue[statsOnly.Count];

                for (var i = 0; i < statsOnly.Count; i++)
                {
                    var defaultValue = 1f;
                    foreach (var stat in previous)
                        if (stat.Stat == statsOnly[i])
                            defaultValue = stat.BaseValue;
                    CharacterStats[i] = new CharacterStatBaseValue(statsOnly[i], defaultValue);
                }
            }

            if (CharacterResources.Length != resourcesOnly.Length)
            {
                var previousResourceValues = CharacterResources;
                CharacterResources = new CharacterStatBaseValue[] {
                    new CharacterStatBaseValue(StatName.Health, 100),
                    new CharacterStatBaseValue(StatName.Resource, 100),
                    new CharacterStatBaseValue(StatName.Shield, 0),
                    new CharacterStatBaseValue(StatName.Experience, 0),
                };

                for (var i = 0; i < previousResourceValues.Length; i++)
                    for (var j = 0; j < CharacterResources.Length; j++)
                        if (previousResourceValues[i].Stat == CharacterResources[j].Stat)
                            CharacterResources[j] = new CharacterStatBaseValue(CharacterResources[j].Stat, previousResourceValues[i].BaseValue);
            }
        }

        [Serializable]
        public class CharacterStatBaseValue : ISerializationCallbackReceiver
        {
            [SerializeField, HideInInspector] private string name;
            [field: SerializeField, HideInInspector] public StatName Stat { get; private set; }
            [field: SerializeField] public float BaseValue;

            public void OnBeforeSerialize() => name = ToString();
            public void OnAfterDeserialize() { }

            public CharacterStatBaseValue(StatName statName, float baseValue = 0)
            {
                Stat = statName;
                name = Stat.SplitCamelCase();

                BaseValue = baseValue;
            }

            public override string ToString()
            {
                var isPercent = false;

                var statName = Stat.SplitCamelCase();

                if (statName.Contains("Percent"))
                {
                    statName = statName.Replace(" Percent", "");
                    isPercent = true;
                }

                return $"{statName}: {BaseValue:0.###}{(isPercent ? "%" : "")}";
            }
        }
    }
}
