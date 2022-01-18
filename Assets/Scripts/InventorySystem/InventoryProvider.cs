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
        public static Inventory PlayerInventory;
        public static PlayerEquipment PlayerEquipment;
        public static Inventory PlayerStash;

        [Space]
        [SerializeField] private InventoryDisplay playerInventoryDisplay;
        [SerializeField] private Vector2Int playerInventorySize = new(10, 6);

        [Space]
        [SerializeField] private InventoryDisplay playerEquipmentDisplay;
        [SerializeField] private Vector2Int playerEquipmentSize = new(2, 6);

        [Space]
        [SerializeField] private InventoryDisplay playerStashDisplay;
        [SerializeField] private Vector2Int playerStashSize = new(10, 15);

        [Header("Items")]
        [SerializeField] private Item potion;
        [SerializeField] private Item boots;
        [SerializeField] private Item sword;
        [SerializeField] private Item chestArmor;

        private Slider amountSlider;
        private Item itemToAdd;
        private uint amount => (uint)amountSlider?.value;
        private TextMeshProUGUI amountText;
        private AbstractContainer containerToAddTo;


        void SetInventories()
        {
            playerInventoryDisplay.SetupInventory(PlayerInventory);
            playerEquipmentDisplay.SetupInventory(PlayerEquipment);
            playerStashDisplay.SetupInventory(PlayerStash);
        }

        [ContextMenu("Awake")]
        public void Awake()
        {
            amountSlider = GetComponentInChildren<Slider>();
            amountText = amountSlider?.GetComponentInChildren<TextMeshProUGUI>();

            // serialize inventories here
            PlayerInventory = new(playerInventorySize);
            PlayerEquipment = new(playerEquipmentSize);
            PlayerStash = new(playerStashSize);

            itemToAdd = potion;
            containerToAddTo = PlayerInventory;

            SetInventories();
        }

        [ContextMenu("AddItem")]
        public void AddItem() => containerToAddTo?.AddToContainer(new Package(itemToAdd, amount));

        [ContextMenu("RemoveItem")]
        public void RemoveItem() => containerToAddTo?.RemoveFromContainer(new Package(itemToAdd, amount));

        [ContextMenu("RemoveAllItems")]
        public void RemoveAllItems()
        {
            List<Vector2Int> positions = containerToAddTo?.StoredPackages.Keys.ToList();
            foreach (var position in positions)
                containerToAddTo.RemoveAtPosition(position);
        }

        public void SetAmountText() => amountText.text = amountSlider.value.ToString();

        public void AddToPlayerInventory() => containerToAddTo = PlayerInventory;
        public void AddToPlayerEquipment() => containerToAddTo = PlayerEquipment;
        public void AddToPlayerStash() => containerToAddTo = PlayerStash;

        public void SetItemToPotion() => itemToAdd = potion;
        public void SetItemToBoots() => itemToAdd = boots;
        public void SetItemToSword() => itemToAdd = sword;
        public void SetItemToChestArmor() => itemToAdd = chestArmor;

    }

}
