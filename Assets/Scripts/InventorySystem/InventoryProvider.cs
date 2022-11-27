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
        public PlayerEquipment PlayerEquipment;
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
        [SerializeField] private List<AbstractItemObject> Arrows;
        [SerializeField] private List<AbstractItemObject> Belts;
        [SerializeField] private List<AbstractItemObject> Books;
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
        [SerializeField] private List<AbstractItemObject> Potions;
        [SerializeField] private List<AbstractItemObject> Weapon1H;
        [SerializeField] private List<AbstractItemObject> Weapon2H;

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
        }

        private void AddRemoveItem(List<AbstractItemObject> items)
        {
            for (var i = 0; i < Amount; i++)
            {
                if (add)
                {
                    random = Random.Range(0, items.Count);
                    _ = ContainerToAddTo?.AddToContainer(new Package(items[random], 1));
                }
                else
                {
                    for (var x = items.Count; x-- > 0;)
                    {
                        var removedPackage = ContainerToAddTo.RemoveFromContainer(new Package(items[x], 1));

                        if (removedPackage.Amount == 0)
                            break;
                    }
                }
            }
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


        // when there is a loot drop event (enemy died, destructable destroyed, chest opened, etc)
        // calculate an amount of loot to drop
        // this could be based on character level, enemy level, area level...
        // ...
        // => adjust amount of drops by magic find?
        // => defines weighted rarity => chests more likely to drop rare/legendary...
        // weighted LOOT TYPE
        // => consumables, equipment, crafting materials, skill, gold...

        [SerializeField] private int dropChanceConsumable;
        [SerializeField] private int dropChanceEquipment;
        private int Sum => dropChanceConsumable + dropChanceEquipment;

        public List<AbstractItemObject> GenerateRandomLoot(int amount = 1)
        {
            #region GENERAL
            // => get the list of all attributes in the game
            var allAttributes = new List<AbstractItemObject>();
            // => exclude non valid attributes down the loot creation
            #endregion

            #region LOOT LEVEL
            // => character level sensitive
            // => define min/max range
            var lootLevel = Character.Instance.CharacterLevel;
            #endregion

            #region LOOT TYPE
            var randomLootType = Random.Range(0, Sum);
            // => exclusion of non relevant attributes
            #endregion

            #region weighted RARITY / QUALITY
            // => adjust rarity selection by magic find ?
            var randomRarity = GetRandomRarity(0);
            // => rarity defines the amount of attributes on an item
            var attributeAmount = randomRarity switch
            {
                ItemRarity.NONE => 0,
                ItemRarity.Crafted => 0,
                ItemRarity.Common => 1,
                ItemRarity.Uncommon => 1,
                ItemRarity.Magic => 2,
                ItemRarity.Rare => 3,
                ItemRarity.Set => 2,    // plus set attributes
                ItemRarity.Unique => 3, // plus unique stats
                _ => 0,
            };
            // => exclusion of non relevant attributes ???
            var rarityAllowedAttributes = new List<AbstractItemObject>();

            foreach (var item in allAttributes)
                if (!rarityAllowedAttributes.Contains(item))
                    allAttributes.Remove(item);

            // allAttributes.RemoveAt(0);
            // => modifies the min/max range of attributes
            var rangeModifier = randomRarity switch
            {
                ItemRarity.NONE => 0f,
                ItemRarity.Crafted => 1f,
                ItemRarity.Common => 1f,
                ItemRarity.Uncommon => 1f,
                ItemRarity.Magic => .9f,
                ItemRarity.Rare => .8f,
                ItemRarity.Set => .8f,
                ItemRarity.Unique => .7f,
                _ => 0f,
            };
            #endregion

            #region weighted EQUIPMENT TYPE
            // => further exclusion of non relevant attributes
            // => character class sensitive
            #endregion

            #region weighted LEGENDARIES
            // => weighted lookup table for each item type
            // => apply/add predefined attributes
            // => player level sensitive ?
            #endregion

            #region
            // => add item type predefined attributes
            // => weighting of remaining possible attributes
            #endregion

            #region
            // get a random number between 0 and the sum of all rarity chances
            // return the highest rarity thats chance <= to the random roll
            #endregion

            #region weighted ATTRIBUTES
            // break things down to keep modifiers impactfull -> noone needs +1% block chance...
            // => double-rolled attributes?
            // -> requires further design
            // => primary/secondary attributes
            // -> requires further design
            #endregion

            #region weighted RANDOM ROLL
            // => more likey to randomly roll a lower range value within min and max
            // => modified by rarity
            // => modified by character level?
            #endregion

            #region REQUIREMENTS / ITEM VALUE
            // => these are derived values from the random modifiers
            #endregion

            return new List<AbstractItemObject> { };

            static ItemRarity GetRandomRarity(int magicFind = 0)
            {
                var randomRoll = Random.Range(0, (int)ItemRarity.Unique); // get rarity dispribution
                randomRoll += magicFind;

                var rarity = ItemRarity.NONE;

                if (randomRoll <= (int)ItemRarity.Common)
                    rarity = ItemRarity.Common;
                else
                if (randomRoll <= (int)ItemRarity.Uncommon)
                    rarity = ItemRarity.Uncommon;
                else
                if (randomRoll <= (int)ItemRarity.Magic)
                    rarity = ItemRarity.Magic;
                else
                if (randomRoll <= (int)ItemRarity.Rare)
                    rarity = ItemRarity.Rare;
                else
                if (randomRoll <= (int)ItemRarity.Set)
                    rarity = ItemRarity.Set;
                else
                if (randomRoll <= (int)ItemRarity.Unique)
                    rarity = ItemRarity.Unique;

                return rarity;
            }
        }
        #endregion
    }
}
