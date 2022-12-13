using System.Collections.Generic;
using System.Linq;
using TeppichsTools.Creation;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Displays;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Inventories
{
    public class InventoryProvider : MonoSingleton<InventoryProvider>
    {
        public PlayerInventory PlayerInventory;
        public PlayerInventory PlayerStash;
        public CharacterEquipment PlayerEquipment;
        public AbstractDimensionalContainer ContainerToAddTo { get; private set; }

        [field: SerializeField] public bool ShowDebugPositions { get; private set; }

        [Space]
        public InventoryContainerDisplay PlayerInventoryDisplay;
        [SerializeField] private Vector2Int playerInventorySize = new(10, 6);

        [Space]
        public InventoryContainerDisplay PlayerStashDisplay;
        [SerializeField] private Vector2Int playerStashSize = new(10, 15);

        [Space]
        public EquipmentContainerDisplay PlayerEquipmentDisplay;
        [SerializeField] private Vector2Int playerEquipmentSize = new(13, 1);

        [Header("Items")]
        [SerializeField] private List<AbstractItemObject> Amulets;
        [SerializeField] private List<AbstractItemObject> Belts;
        [SerializeField] private List<AbstractItemObject> Boots;
        [SerializeField] private List<AbstractItemObject> Bracers;
        [SerializeField] private List<AbstractItemObject> Chests;
        [SerializeField] private List<AbstractItemObject> Cloaks;
        [SerializeField] private List<AbstractItemObject> Gloves;
        [SerializeField] private List<AbstractItemObject> Helmets;
        [SerializeField] private List<AbstractItemObject> Pants;
        [SerializeField] private List<AbstractItemObject> Quiver;
        [SerializeField] private List<AbstractItemObject> Rings;
        [SerializeField] private List<AbstractItemObject> Shields;
        [SerializeField] private List<AbstractItemObject> Shoulder;
        [SerializeField] private List<AbstractItemObject> Weapon1H;
        [SerializeField] private List<AbstractItemObject> Weapon2H;
        [Space]
        [SerializeField] private List<AbstractItemObject> Arrows;
        [SerializeField] private List<AbstractItemObject> Books;
        [SerializeField] private List<AbstractItemObject> Potions;

        private static List<AbstractItemObject> _Amulets;
        private static List<AbstractItemObject> _Belts;
        private static List<AbstractItemObject> _Boots;
        private static List<AbstractItemObject> _Bracers;
        private static List<AbstractItemObject> _Chests;
        private static List<AbstractItemObject> _Cloaks;
        private static List<AbstractItemObject> _Gloves;
        private static List<AbstractItemObject> _Helmets;
        private static List<AbstractItemObject> _Pants;
        private static List<AbstractItemObject> _Quiver;
        private static List<AbstractItemObject> _Rings;
        private static List<AbstractItemObject> _Shields;
        private static List<AbstractItemObject> _Shoulder;
        private static List<AbstractItemObject> _Weapon1H;
        private static List<AbstractItemObject> _Weapon2H;

        private static List<AbstractItemObject> _Arrows;
        private static List<AbstractItemObject> _Books;
        private static List<AbstractItemObject> _Potions;

        [SerializeField] private bool add = true;

        private Slider amountSlider;
        private TextMeshProUGUI amountText;
        private uint Amount => amountSlider != null ? (uint)amountSlider.value : 1;

        private int random = 0;

        private void SetInventories()
        {
            PlayerInventoryDisplay.SetupDisplay(PlayerInventory);
            PlayerStashDisplay.SetupDisplay(PlayerStash);
            PlayerEquipmentDisplay.SetupDisplay(PlayerEquipment);
        }

        [ContextMenu("Awake")]
        public void Awake()
        {
            amountSlider = GetComponentInChildren<Slider>();
            amountText = amountSlider?.GetComponentInChildren<TextMeshProUGUI>();

            // serialize inventories
            PlayerInventory = new(playerInventorySize);
            PlayerStash = new(playerStashSize);
            PlayerEquipment = new(playerEquipmentSize);

            ContainerToAddTo = PlayerInventory; // should get the current active toggle instead
            add = true;

            SetInventories();

            _Amulets = Amulets;
            _Arrows = Arrows;
            _Belts = Belts;
            _Books = Books;
            _Boots = Boots;
            _Bracers = Bracers;
            _Chests = Chests;
            _Cloaks = Cloaks;
            _Gloves = Gloves;
            _Helmets = Helmets;
            _Pants = Pants;
            _Quiver = Quiver;
            _Rings = Rings;
            _Shields = Shields;
            _Shoulder = Shoulder;
            _Potions = Potions;
            _Weapon1H = Weapon1H;
            _Weapon2H = Weapon2H;
        }

        private void AddRemoveItem(List<AbstractItemObject> objects)
        {
            for (var i = 0; i < Amount; i++)
            {
                if (add)
                {
                    random = Random.Range(0, objects.Count);
                    _ = ContainerToAddTo?.AddToContainer(new Package(objects[random].GetItem(), 1));
                }
                else
                {
                    for (var x = objects.Count; x-- > 0;)
                    {
                        var removedPackage = ContainerToAddTo.RemoveFromContainer(new Package(objects[x].GetItem(), 1));

                        if (removedPackage.Amount == 0)
                            break;
                    }
                }
            }
        }

        public void AddRandomLoot()
        {
            var items = GenerateLoot(Amount);

            for (var i = 0; i < items.Count; i++)
                if (add)
                    _ = ContainerToAddTo?.AddToContainer(new Package(items[i], 1));
        }


        [ContextMenu("RemoveAllItems")]
        public void RemoveAllItems()
        {
            var storedPackages = ContainerToAddTo?.StoredPackages.ToList();
            for (var i = 0; i < storedPackages.Count; i++)
                _ = ContainerToAddTo.RemoveAtPosition(storedPackages[i].Key, storedPackages[i].Value);
        }

        public void SetAmountText() => amountText.text = amountSlider.value.ToString();

        public void AddToPlayerInventory()
        {
            ContainerToAddTo = PlayerInventory;
            SetInventories();
        }
        public void AddToPlayerEquipment()
        {
            ContainerToAddTo = PlayerEquipment;
            SetInventories();
        }
        public void AddToPlayerStash()
        {
            ContainerToAddTo = PlayerStash;
            SetInventories();
        }

        public void SetItemToAmulets() => AddRemoveItem(Amulets);
        public void SetItemToArrows() => AddRemoveItem(Arrows);
        public void SetItemToBelts() => AddRemoveItem(Belts);
        public void SetItemToBooks() => AddRemoveItem(Books);
        public void SetItemToBoots() => AddRemoveItem(Boots);
        public void SetItemToBracers() => AddRemoveItem(Bracers);
        public void SetItemToChests() => AddRemoveItem(Chests);
        public void SetItemToCloaks() => AddRemoveItem(Cloaks);
        public void SetItemToGloves() => AddRemoveItem(Gloves);
        public void SetItemToHelmets() => AddRemoveItem(Helmets);
        public void SetItemToPants() => AddRemoveItem(Pants);
        public void SetItemToQuiver() => AddRemoveItem(Quiver);
        public void SetItemToRings() => AddRemoveItem(Rings);
        public void SetItemToShields() => AddRemoveItem(Shields);
        public void SetItemToShoulders() => AddRemoveItem(Shoulder);
        public void SetItemToPotions() => AddRemoveItem(Potions);
        public void SetItemToWeapon1H() => AddRemoveItem(Weapon1H);
        public void SetItemToWeapon2H() => AddRemoveItem(Weapon2H);

        public void SetAddRemove() => add = !add;

        public void SetAutoEquip() => PlayerEquipment.autoEquip = !PlayerEquipment.autoEquip;

        public void SortInventory() => ContainerToAddTo.SortByItemDimension();

        public Package EquipItem(Package package) => PlayerEquipment.AddToContainer(package);

        #region LOOT GENERATION
        public List<AbstractItem> GenerateLoot(uint amount = 1)
        {
            var generatedLoot = new List<AbstractItem>();
            /// calculates the number of items to drop
            CalculateBonusDrops(ref amount);

            for (var i = 0; i < amount; i++)
                generatedLoot.Add(GenerateItem());

            return generatedLoot;

            static void CalculateBonusDrops(ref uint amount)
            {
                var bonusDrops = Character.Instance.GetStatValue(StatName.IncreasedItemQuantityPercent);
                amount += (uint)(bonusDrops / 100f); // TODO: requires a better formula
            }
        }

        [SerializeField] private ItemCategoryDistribution itemCategoryDistribution;
        [SerializeField] private ItemRarityDistribution itemRarityDistribution;
        [SerializeField] private EquipmentCategoryDistribution equipmentCategoryDistribution;
        [SerializeField] private EquipmentTypeDistribution armamentsDistribution;
        [SerializeField] private EquipmentTypeDistribution weaponsDistribution;
        [SerializeField] private EquipmentTypeDistribution jewelryDistribution;
        [SerializeField] private ConsumableTypeDistribution consumableTypeDistribution;

        private AbstractItem GenerateItem()
        {
            /// selects item type
            var itemCategory = itemCategoryDistribution.GetRandomEnumerator();

            if (itemCategory == ItemCategory.NONE)
                return null;

            /// selects item qualities
            var itemRarity = itemRarityDistribution.GetRandomEnumerator();

            if (itemRarity == ItemRarity.NoDrop)
                return null;

            if (itemCategory == ItemCategory.Equipment)
            {
                var equipmentCategory = equipmentCategoryDistribution.GetRandomEnumerator();
                // then select equipmentType within that Category

                if (equipmentCategory == EquipmentCategory.NONE)
                    return null;

                var equipmentType = equipmentCategory switch
                {
                    EquipmentCategory.Armaments => armamentsDistribution.GetRandomEnumerator(),
                    EquipmentCategory.Weapons => weaponsDistribution.GetRandomEnumerator(), // if weapons -> determine weapon category -> pick within that category
                    EquipmentCategory.Jewelry => jewelryDistribution.GetRandomEnumerator(),

                    EquipmentCategory.NONE => EquipmentType.NONE,
                    _ => EquipmentType.NONE,
                };

                return equipmentType == EquipmentType.NONE ? null : (AbstractItem)new EquipmentItem(equipmentType, itemRarity);
            }
            else if (itemCategory == ItemCategory.Consumable)
            {
                var consumable = consumableTypeDistribution.GetRandomEnumerator();

                return consumable == ConsumableType.NONE ? null : (AbstractItem)new ConsumableItem(consumable, itemRarity);
            }

            //////// old version below
            void AddRandomAffixes(ref List<PlayerStatModifier> affixes)
            {
                var allowedAffixes = new List<StatName>();
                /// selects item properties
                //for (var i = 0; i < randomStatAmount; i++)
                {
                    /// weighted STATS
                    // => primary/secondary stats? -> requires further design
                    // => double-rolled stats? -> requires further design

                    var randomRoll = Random.Range(0, allowedAffixes.Count);
                    var randomStat = allowedAffixes[randomRoll];
                    allowedAffixes.RemoveAt(randomRoll); // to exclude double rolls

                    /// weighted RANDOM ROLL
                    var lootLevel = Character.Instance.CharacterLevel; // define base min/max stat range
                    var minMax = Vector2.up; // as min/max is different for each stat we need a lookup table to convert the lootLevel into min/max ranges

                    var rangeModifier = itemRarity switch
                    {
                        ItemRarity.NoDrop => 0f,
                        //ItemRarity.Crafted => 1f,
                        ItemRarity.Common => 1f,
                        //ItemRarity.Uncommon => 1f,
                        ItemRarity.Magic => .9f,
                        ItemRarity.Rare => .8f,
                        //ItemRarity.Set => .8f,
                        ItemRarity.Unique => .7f,
                        _ => 0f,
                    };

                    minMax *= rangeModifier;

                    var value = Random.Range(minMax.x, minMax.y);                           // TODO: should favor lower range value within min and max
                    var statModifier = new StatModifier(value, StatModifierType.FlatAdd); // TODO: lookup table for each statName => rework the statModifier to derive the type from the name?
                    var itemStat = new PlayerStatModifier(randomStat, statModifier);

                    //affixList.Add(itemStat);
                }
            }

            return null;
        }

        private static AbstractItemObject GetRandomUnique(EquipmentType type)
        {
            var uniquesOfType = type switch
            {
                EquipmentType.Amulet => _Amulets,
                EquipmentType.Belt => _Belts,
                EquipmentType.Boots => _Boots,
                EquipmentType.Bracers => _Bracers,
                EquipmentType.Chest => _Chests,
                EquipmentType.Cloak => _Cloaks,
                EquipmentType.Gloves => _Gloves,
                EquipmentType.Helm => _Helmets,
                EquipmentType.Pants => _Pants,
                EquipmentType.Quiver => _Quiver,
                EquipmentType.Ring => _Rings,
                EquipmentType.Shield => _Shields,
                EquipmentType.Shoulders => _Shoulder,
                // EquipmentType.Weapon_1H => _Weapon1H,
                // EquipmentType.Weapon_2H => _Weapon2H,

                EquipmentType.NONE => null,
                _ => null,
            };

            // TODO: weighted lookup table for each item type
            // => player level sensitive ?

            var index = Random.Range(0, uniquesOfType.Count);
            return uniquesOfType[index];
        }

        #endregion
    }
}
