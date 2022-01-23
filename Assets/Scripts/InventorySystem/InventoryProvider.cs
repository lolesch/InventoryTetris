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
        [SerializeField] private List<Item> Amulet;
        [SerializeField] private List<Item> Belt;
        [SerializeField] private List<Item> Boots;
        [SerializeField] private List<Item> Bracers;
        [SerializeField] private List<Item> Chest;
        [SerializeField] private List<Item> Gloves;
        [SerializeField] private List<Item> Helm;
        [SerializeField] private List<Item> OffHand;
        [SerializeField] private List<Item> Pants;
        [SerializeField] private List<Item> Potion;
        [SerializeField] private List<Item> Ring;
        [SerializeField] private List<Item> Weapon1H;

        private Slider amountSlider;
        private List<Item> itemToAdd;
        private uint amount => (uint)amountSlider?.value;
        private TextMeshProUGUI amountText;
        private AbstractDimensionalContainer containerToAddTo;

        private int current = 0;

        void SetInventories()
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

            itemToAdd = Amulet;
            containerToAddTo = PlayerInventory;

            SetInventories();
        }

        [ContextMenu("AddItem")]
        public void AddItem()
        {
            for (int i = 0; i < amount; i++)
            {
                containerToAddTo?.AddToContainer(new Package(itemToAdd[Mathf.Abs(current % itemToAdd.Count)], 1));
                current++;
            }
        }

        [ContextMenu("RemoveItem")]
        public void RemoveItem()
        {
            for (int i = 0; i < amount; i++)
            {
                current--;
                containerToAddTo?.RemoveFromContainer(new Package(itemToAdd[Mathf.Abs(current % itemToAdd.Count)], 1));
            }
        }

        [ContextMenu("RemoveAllItems")]
        public void RemoveAllItems()
        {
            var storedPackages = containerToAddTo?.storedPackages.ToList();
            for (int i = 0; i < storedPackages.Count; i++)
                containerToAddTo.RemoveItemAtPosition(storedPackages[i].Key, storedPackages[i].Value);
        }

        public void SetAmountText() => amountText.text = amountSlider.value.ToString();

        public void AddToPlayerInventory() => containerToAddTo = PlayerInventory;
        public void AddToPlayerEquipment() => containerToAddTo = PlayerEquipment;
        public void AddToPlayerStash() => containerToAddTo = PlayerStash;

        public void SetItemToAmulet() => itemToAdd = Amulet;
        public void SetItemToBelt() => itemToAdd = Belt;
        public void SetItemToBoots() => itemToAdd = Boots;
        public void SetItemToBracers() => itemToAdd = Bracers;
        public void SetItemToChest() => itemToAdd = Chest;
        public void SetItemToGloves() => itemToAdd = Gloves;
        public void SetItemToHelm() => itemToAdd = Helm;
        public void SetItemToOffHand() => itemToAdd = OffHand;
        public void SetItemToPants() => itemToAdd = Pants;
        public void SetItemToPotion() => itemToAdd = Potion;
        public void SetItemToRing() => itemToAdd = Ring;
        public void SetItemToSword() => itemToAdd = Weapon1H;

        public void SortInventory() => containerToAddTo.Sort();

        public static Package EquipItem(Package package) => PlayerEquipment.AddToContainer(package);

    }

}
