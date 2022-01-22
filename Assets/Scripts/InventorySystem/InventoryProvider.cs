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
        [SerializeField] private Vector2Int playerEquipmentSize = new(13, 0);

        [Header("Items")]
        [SerializeField] private Item potion;
        [SerializeField] private Item boots;
        [SerializeField] private Item sword;
        [SerializeField] private Item chestArmor;

        private Slider amountSlider;
        private Item itemToAdd;
        private uint amount => (uint)amountSlider?.value;
        private TextMeshProUGUI amountText;
        private AbstractDimensionalContainer containerToAddTo;


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
            var storedPackages = containerToAddTo?.storedPackages.ToList();
            for (int i = 0; i < storedPackages.Count; i++)
                containerToAddTo.RemoveItemAtPosition(storedPackages[i].Key, storedPackages[i].Value);
        }

        public void SetAmountText() => amountText.text = amountSlider.value.ToString();

        public void AddToPlayerInventory() => containerToAddTo = PlayerInventory;
        public void AddToPlayerEquipment() => containerToAddTo = PlayerEquipment;
        public void AddToPlayerStash() => containerToAddTo = PlayerStash;

        public void SetItemToPotion() => itemToAdd = potion;
        public void SetItemToBoots() => itemToAdd = boots;
        public void SetItemToSword() => itemToAdd = sword;
        public void SetItemToChestArmor() => itemToAdd = chestArmor;

        public void SortInventory() => containerToAddTo.Sort();

    }

}
