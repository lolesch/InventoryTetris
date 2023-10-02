using System.Linq;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.GUI.InventoryDisplays;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Inventories
{
    public class InventoryProvider : AbstractProvider<InventoryProvider>
    {
        [field: SerializeField] public CharacterInventory Inventory { get; private set; }
        [field: SerializeField] public CharacterInventory Stash { get; private set; }
        [field: SerializeField] public CharacterEquipment Equipment { get; private set; }
        [field: SerializeField] public AbstractDimensionalContainer ContainerToAddTo { get; private set; }

        [field: SerializeField] public bool ShowDebugPositions { get; private set; }

        [Space]
        public InventoryContainerDisplay InventoryDisplay;
        [SerializeField] private Vector2Int inventorySize = new(10, 6);

        [Space]
        public InventoryContainerDisplay StashDisplay;
        [SerializeField] private Vector2Int stashSize = new(10, 15);

        [Space]
        public EquipmentContainerDisplay EquipmentDisplay;
        [SerializeField] private Vector2Int equipmentSize = new(14, 1);

        [SerializeField] private Slider amountSlider;
        [SerializeField] private TextMeshProUGUI amountText;
        private uint Amount => amountSlider != null ? (uint)amountSlider.value : 1;

        private void SetInventories()
        {
            InventoryDisplay.SetupDisplay(Inventory);
            StashDisplay.SetupDisplay(Stash);
            EquipmentDisplay.SetupDisplay(Equipment);
        }

        [ContextMenu("Awake")]
        public void Awake()
        {
            //amountSlider = GetComponentInChildren<Slider>();
            //amountText = amountSlider?.GetComponentInChildren<TextMeshProUGUI>();

            /// serialize inventories
            Inventory = new(inventorySize);
            Stash = new(stashSize);
            Equipment = new(equipmentSize);

            ContainerToAddTo = Inventory; // should get the current active toggle instead

            SetInventories();
        }

        private void AddRemoveEquipment(EquipmentType equipmentType)
        {
            //Debug.Log($"amountToAdd? {Amount}");
            for (var i = 0; i < Amount; i++)
            {
                var randomEquipment = ItemProvider.Instance.GenerateRandomOfEquipmentType(equipmentType);
                //Debug.Log($"{ContainerToAddTo}");
                _ = ContainerToAddTo?.AddToContainer(new Package(null, randomEquipment, 1));
            }
        }

        private void AddRemoveConsumable(ConsumableType consumableType)
        {
            for (var i = 0; i < Amount; i++)
            {
                var randomConsumable = ItemProvider.Instance.GenerateRandomOfConsumableType(consumableType);
                _ = ContainerToAddTo?.AddToContainer(new Package(null, randomConsumable, 1));
            }
        }

        public void AddRemoveRandomCurrency()
        {
            for (var i = 0; i < Amount; i++)
            {
                var randomCurrency = ItemProvider.Instance.GenerateRandomCurrency();
                _ = ContainerToAddTo?.AddToContainer(new Package(null, randomCurrency, 1));
            }

        }
        private void AddRemoveCurrency(CurrencyType currencyType)
        {
            for (var i = 0; i < Amount; i++)
            {
                var randomCurrency = ItemProvider.Instance.GenerateRandomOfCurrencyType(currencyType);
                _ = ContainerToAddTo?.AddToContainer(new Package(null, randomCurrency, 1));
            }
        }

        public void AddRemoveRandomLoot()
        {
            var items = ItemProvider.Instance.GenerateRandomLoot(Amount);

            for (var i = 0; i < items.Count; i++)
                _ = ContainerToAddTo?.AddToContainer(new Package(null, items[i], 1));
        }

        public void RemoveAllItems(AbstractDimensionalContainer container)
        {
            var storedPackages = container?.StoredPackages.ToList();
            for (var i = 0; i < storedPackages.Count; i++)
                _ = container.RemoveAtPosition(storedPackages[i].Key, storedPackages[i].Value);
        }

        public void SetAmountText() => amountText.text = amountSlider.value.ToString();

        public void AddToPlayerInventory()
        {
            ContainerToAddTo = Inventory;
            SetInventories();
        }
        public void AddToPlayerEquipment()
        {
            ContainerToAddTo = Equipment;
            SetInventories();
        }
        public void AddToPlayerStash()
        {
            ContainerToAddTo = Stash;
            SetInventories();
        }

        public void SetItemToAmulets() => AddRemoveEquipment(EquipmentType.Amulet);
        public void SetItemToBelts() => AddRemoveEquipment(EquipmentType.Belt);
        public void SetItemToBoots() => AddRemoveEquipment(EquipmentType.Boots);
        public void SetItemToBracers() => AddRemoveEquipment(EquipmentType.Bracers);
        public void SetItemToChests() => AddRemoveEquipment(EquipmentType.Chest);
        public void SetItemToCloaks() => AddRemoveEquipment(EquipmentType.Cloak);
        public void SetItemToGloves() => AddRemoveEquipment(EquipmentType.Gloves);
        public void SetItemToHelmets() => AddRemoveEquipment(EquipmentType.Helm);
        public void SetItemToPants() => AddRemoveEquipment(EquipmentType.Pants);
        public void SetItemToQuiver() => AddRemoveEquipment(EquipmentType.Quiver);
        public void SetItemToRings() => AddRemoveEquipment(EquipmentType.Ring);
        public void SetItemToShields() => AddRemoveEquipment(EquipmentType.Shield);
        public void SetItemToShoulders() => AddRemoveEquipment(EquipmentType.Shoulders);
        public void SetItemToWeapon1H() => AddRemoveEquipment(EquipmentType.ONEHANDEDWEAPONS);
        public void SetItemToWeapon2H() => AddRemoveEquipment(EquipmentType.TWOHANDEDWEAPONS);

        public void SetItemToArrows() => AddRemoveConsumable(ConsumableType.Arrow);
        public void SetItemToBooks() => AddRemoveConsumable(ConsumableType.Book);
        public void SetItemToPotions() => AddRemoveConsumable(ConsumableType.Potion);

        public void SetItemToIron() => AddRemoveCurrency(CurrencyType.Iron);
        public void SetItemToCopper() => AddRemoveCurrency(CurrencyType.Copper);
        public void SetItemToSilver() => AddRemoveCurrency(CurrencyType.Silver);
        public void SetItemToGold() => AddRemoveCurrency(CurrencyType.Gold);

        public void ToggleAutoEquip() => Equipment.autoEquip = !Equipment.autoEquip;

        //public void SortSelectedInventory() => ContainerToAddTo.Sort();
        public void SortPlayerInventory() => Inventory.Sort();
        public void SortPlayerStash() => Stash.Sort();

        public void ClearPlayerInventory() => RemoveAllItems(Inventory);
        public void ClearPlayerStash() => RemoveAllItems(Stash);
        public void ClearPlayerEquipment() => RemoveAllItems(Equipment);

        //public Package EquipItem(Package package) => PlayerEquipment.AddToContainer(package);
    }
}
