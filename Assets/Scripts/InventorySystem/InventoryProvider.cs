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

        //public void SetToAddItems() => add = true;
        //public void SetToRemoveItems() => add = false;
        public void SetAddRemove() => add = !add;

        public void SetAutoEquip() => PlayerEquipment.autoEquip = !PlayerEquipment.autoEquip;

        public void SortInventory() => ContainerToAddTo.SortByItemDimension();

        public Package EquipItem(Package package) => PlayerEquipment.AddToContainer(package);
    }
}
