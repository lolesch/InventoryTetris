using System.Collections.Generic;
using System.Linq;
using TeppichsTools.Creation;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Displays;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Inventories
{
    public class InventoryProvider : MonoSingleton<InventoryProvider>
    {
        public static PlayerInventory PlayerInventory;
        public static PlayerInventory PlayerStash;
        public static PlayerEquipment PlayerEquipment;

        [Space]
        [SerializeField] private InventoryContainerDisplay playerInventoryDisplay;
        [SerializeField] private Vector2Int playerInventorySize = new(10, 6);

        [Space]
        [SerializeField] private InventoryContainerDisplay playerStashDisplay;
        [SerializeField] private Vector2Int playerStashSize = new(10, 15);

        [Space]
        [SerializeField] private EquipmentContainerDisplay playerEquipmentDisplay;
        [SerializeField] private Vector2Int playerEquipmentSize = new(13, 1);

        [Header("Items")]
        [SerializeField] private List<Item> Amulets;
        [SerializeField] private List<Item> Arrows;
        [SerializeField] private List<Item> Belts;
        [SerializeField] private List<Item> Books;
        [SerializeField] private List<Item> Boots;
        [SerializeField] private List<Item> Bracers;
        [SerializeField] private List<Item> Chests;
        [SerializeField] private List<Item> Cloaks;
        [SerializeField] private List<Item> Gloves;
        [SerializeField] private List<Item> Helmets;
        [SerializeField] private List<Item> Pants;
        [SerializeField] private List<Item> Quiver;
        [SerializeField] private List<Item> Rings;
        [SerializeField] private List<Item> Shields;
        [SerializeField] private List<Item> Shoulder;
        [SerializeField] private List<Item> Potions;
        [SerializeField] private List<Item> Weapon1H;
        [SerializeField] private List<Item> Weapon2H;

        private Slider amountSlider;
        private List<Item> itemToAdd;
        private uint amount => (uint)amountSlider?.value;
        private TextMeshProUGUI amountText;
        private AbstractDimensionalContainer containerToAddTo;

        private int current = 0;

        private void SetInventories()
        {
            playerInventoryDisplay.SetupDisplay(PlayerInventory);
            playerStashDisplay.SetupDisplay(PlayerStash);
            playerEquipmentDisplay.SetupDisplay(PlayerEquipment);
        }

        [ContextMenu("Awake")]
        public void Awake()
        {
            amountSlider = GetComponentInChildren<Slider>();
            amountText = amountSlider?.GetComponentInChildren<TextMeshProUGUI>();

            // serialize inventories here
            PlayerInventory = new(playerInventorySize);
            PlayerStash = new(playerStashSize);
            PlayerEquipment = new(playerEquipmentSize);

            itemToAdd = Belts; // should get the current active toggle instead
            containerToAddTo = PlayerInventory;

            SetInventories();
        }

        [ContextMenu("AddItem")]
        public void AddItem()
        {
            for (var i = 0; i < amount; i++)
            {
                containerToAddTo?.AddToContainer(new Package(itemToAdd[Mathf.Abs(current % itemToAdd.Count)], 1));
                current++;
            }
        }

        [ContextMenu("RemoveItem")]
        public void RemoveItem()
        {
            for (var i = 0; i < amount; i++)
            {
                current--;
                containerToAddTo?.RemoveFromContainer(new Package(itemToAdd[Mathf.Abs(current % itemToAdd.Count)], 1));
            }
        }

        [ContextMenu("RemoveAllItems")]
        public void RemoveAllItems()
        {
            var storedPackages = containerToAddTo?.storedPackages.ToList();
            for (var i = 0; i < storedPackages.Count; i++)
                containerToAddTo.RemoveItemAtPosition(storedPackages[i].Key, storedPackages[i].Value);
        }

        public void SetAmountText() => amountText.text = amountSlider.value.ToString();

        public void AddToPlayerInventory() => containerToAddTo = PlayerInventory;
        public void AddToPlayerEquipment() => containerToAddTo = PlayerEquipment;
        public void AddToPlayerStash() => containerToAddTo = PlayerStash;

        public void SetItemToAmulets() => itemToAdd = Amulets;
        public void SetItemToArrows() => itemToAdd = Arrows;
        public void SetItemToBelts() => itemToAdd = Belts;
        public void SetItemToBooks() => itemToAdd = Books;
        public void SetItemToBoots() => itemToAdd = Boots;
        public void SetItemToBracers() => itemToAdd = Bracers;
        public void SetItemToChests() => itemToAdd = Chests;
        public void SetItemToCloaks() => itemToAdd = Cloaks;
        public void SetItemToGloves() => itemToAdd = Gloves;
        public void SetItemToHelmets() => itemToAdd = Helmets;
        public void SetItemToPants() => itemToAdd = Pants;
        public void SetItemToQuiver() => itemToAdd = Quiver;
        public void SetItemToRings() => itemToAdd = Rings;
        public void SetItemToShields() => itemToAdd = Shields;
        public void SetItemToShoulders() => itemToAdd = Shoulder;
        public void SetItemToPotions() => itemToAdd = Potions;
        public void SetItemToWeapon1H() => itemToAdd = Weapon1H;
        public void SetItemToWeapon2H() => itemToAdd = Weapon2H;

        public void SortInventory() => containerToAddTo.SortByItemDimension();

        public static Package EquipItem(Package package) => PlayerEquipment.AddToContainer(package);

    }

}
