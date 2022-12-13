using System;
using System.Linq;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "Item Type Data", menuName = "Inventory System/ItemType Data")]
    public class ItemTypeData : ScriptableObject
    {
        [Serializable]
        public struct StatRange
        {
            [SerializeField, HideInInspector] public string name;
            [SerializeField, HideInInspector] public StatName StatName;
            [SerializeField] public Vector2Int MinMax;

            public StatRange(StatName statName, Vector2Int minMax)
            {
                StatName = statName;
                MinMax = minMax;

                name = StatName.ToString();
            }
        }

        [Serializable]
        public struct TypeSpecificStatRange
        {
            [SerializeField] public EquipmentType EquipmentType;
            [SerializeField] public StatRange StatRange;
        }

        [field: SerializeField] public StatRange[] PossibleStatRolls { get; private set; } = new StatRange[System.Enum.GetValues(typeof(StatName)).Length];
        [field: SerializeField] public TypeSpecificStatRange[] TypeSpecificStats { get; private set; } = new TypeSpecificStatRange[System.Enum.GetValues(typeof(EquipmentType)).Length];

        private void OnValidate()
        {
            var statNames = System.Enum.GetValues(typeof(StatName)) as StatName[];
            if (PossibleStatRolls.Length != statNames.Length)
                PossibleStatRolls = new StatRange[statNames.Length];

            for (var i = 0; i < statNames.Length; i++)
                PossibleStatRolls[i].StatName = statNames[i];

            var equipmentTypes = System.Enum.GetValues(typeof(EquipmentType)) as EquipmentType[];
            if (TypeSpecificStats.Length != equipmentTypes.Length)
                TypeSpecificStats = new TypeSpecificStatRange[equipmentTypes.Length];

            for (var i = 0; i < equipmentTypes.Length; i++)
                TypeSpecificStats[i].EquipmentType = equipmentTypes[i];
        }

        // TODO: GetItemTypeData
        // contains itemType specific affixes and allowedAffixes, their distribution and minMax range

        public StatRange GetTypeSpecificStat(EquipmentType equipmentType) => TypeSpecificStats.Where(x => x.EquipmentType == equipmentType).FirstOrDefault().StatRange;

        public Sprite GetIcon(EquipmentType equipmentType, ItemRarity rarity) => null;
        public AbstractItemObject GetUnique(EquipmentType equipmentType) => null;
    }
}
