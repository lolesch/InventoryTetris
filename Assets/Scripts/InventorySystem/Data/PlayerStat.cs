using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data
{
    [System.Serializable]
    public class PlayerStat : ISerializationCallbackReceiver
    {
        [HideInInspector, SerializeField] private string name;

        [SerializeField] private float modifiedValue;
        [field: SerializeField] public uint BaseValue { get; private set; }

        [field: SerializeField, HideInInspector] public StatName Stat { get; private set; }
        [field: SerializeField] public List<StatModifier> StatModifiers { get; private set; }

        public PlayerStat(StatName statName, uint baseValue = 0)
        {
            Stat = statName;
            BaseValue = baseValue;
        }

        public void RemoveModifier(StatModifier modifier)
        {
            for (int i = StatModifiers.Count; i-- > 0;)
                if (StatModifiers[i].Equals(modifier))
                    StatModifiers.RemoveAt(i);

            CalculateModifiedValue();
        }

        public void AddModifier(StatModifier modifier)
        {
            StatModifiers.Add(modifier);

            CalculateModifiedValue();
        }

        private void CalculateModifiedValue()
        {
            var result = BaseValue;

            StatModifiers.Sort((x, y) => x.SortByType(y));

            foreach (var mod in StatModifiers)
            //result = mod.Modify(result);
            { }

            modifiedValue = result;
        }

        public void OnBeforeSerialize() => name = Stat.ToString();

        public void OnAfterDeserialize() => name = Stat.ToString();
    }
}
