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
        [field: SerializeField] public CharacterEquipment Equipment { get; private set; }
        [field: SerializeField] public CharacterInventory Inventory { get; private set; }
        [field: SerializeField] public CharacterInventory Stash { get; private set; }
        [field: SerializeField] public CharacterInventory Store { get; private set; }

        [field: SerializeField] public bool ShowDebugPositions { get; private set; }

        [Space]
        public EquipmentContainerDisplay EquipmentDisplay;
        [SerializeField] private Vector2Int equipmentSize = new(14, 1);

        [Space]
        public InventoryContainerDisplay InventoryDisplay;
        [SerializeField] private Vector2Int inventorySize = new(10, 6);

        [Space]
        public InventoryContainerDisplay StashDisplay;
        [SerializeField] private Vector2Int stashSize = new(10, 16);

        [Space]
        public InventoryContainerDisplay StoreDisplay;
        [SerializeField] private Vector2Int storeSize = new(10, 16);

        [SerializeField] private Slider amountSlider;
        [SerializeField] private TextMeshProUGUI amountText;
        private uint Amount => amountSlider != null ? (uint)amountSlider.value : 1;

        private void SetInventories()
        {
            EquipmentDisplay.SetupDisplay(Equipment);
            InventoryDisplay.SetupDisplay(Inventory);
            StashDisplay.SetupDisplay(Stash);

            StoreDisplay.SetupDisplay(Store);
        }

        public void Awake()
        {
            /// serialize inventories
            Equipment = new(equipmentSize);
            Inventory = new(inventorySize);
            Stash = new(stashSize);

            Store = new(storeSize);
            RestockStore();

            SetInventories();
        }

        private void AddEquipment(EquipmentType equipmentType)
        {
            //Debug.Log($"amountToAdd? {Amount}");
            for (var i = 0; i < Amount; i++)
            {
                var randomEquipment = ItemProvider.Instance.GenerateRandomOfEquipmentType(equipmentType);
                //Debug.Log($"{ContainerToAddTo}");
                _ = Inventory?.AddToContainer(new Package(null, randomEquipment, 1));
            }
        }

        private void AddConsumable(ConsumableType consumableType)
        {
            for (var i = 0; i < Amount; i++)
            {
                var randomConsumable = ItemProvider.Instance.GenerateRandomOfConsumableType(consumableType);
                _ = Inventory?.AddToContainer(new Package(null, randomConsumable, 1));
            }
        }

        private void AddCurrency(CurrencyType currencyType)
        {
            for (var i = 0; i < Amount; i++)
            {
                var randomCurrency = ItemProvider.Instance.GenerateCurrency(currencyType);
                _ = Inventory?.AddToContainer(new Package(null, randomCurrency, 1));
            }
        }

        public void AddRandomLoot()
        {
            var items = ItemProvider.Instance.GenerateRandomLoot(Amount);

            for (var i = 0; i < items.Count; i++)
                _ = Inventory?.AddToContainer(new Package(null, items[i], 1));
        }
        public void AddRandomCurrency()
        {
            var randomCurrency = ItemProvider.Instance.GenerateRandomCurrency();

            for (var i = 0; i < Amount; i++)
                _ = Inventory?.AddToContainer(new Package(null, randomCurrency, 1));
        }

        public void RemoveAllItems(AbstractDimensionalContainer container)
        {
            var storedPackages = container?.StoredPackages.ToList();
            for (var i = 0; i < storedPackages.Count; i++)
                _ = container.RemoveAtPosition(storedPackages[i].Key, storedPackages[i].Value);
        }

        public void SetAmountText() => amountText.text = amountSlider.value.ToString();

        public void SetItemToAmulets() => AddEquipment(EquipmentType.Amulet);
        public void SetItemToBelts() => AddEquipment(EquipmentType.Belt);
        public void SetItemToBoots() => AddEquipment(EquipmentType.Boots);
        public void SetItemToBracers() => AddEquipment(EquipmentType.Bracers);
        public void SetItemToChests() => AddEquipment(EquipmentType.Chest);
        public void SetItemToCloaks() => AddEquipment(EquipmentType.Cloak);
        public void SetItemToGloves() => AddEquipment(EquipmentType.Gloves);
        public void SetItemToHelmets() => AddEquipment(EquipmentType.Helm);
        public void SetItemToPants() => AddEquipment(EquipmentType.Pants);
        public void SetItemToQuiver() => AddEquipment(EquipmentType.Quiver);
        public void SetItemToRings() => AddEquipment(EquipmentType.Ring);
        public void SetItemToShields() => AddEquipment(EquipmentType.Shield);
        public void SetItemToShoulders() => AddEquipment(EquipmentType.Shoulders);
        public void SetItemToWeapon1H() => AddEquipment(EquipmentType.ONEHANDEDWEAPONS);
        public void SetItemToWeapon2H() => AddEquipment(EquipmentType.TWOHANDEDWEAPONS);

        public void SetItemToArrows() => AddConsumable(ConsumableType.Arrow);
        public void SetItemToBooks() => AddConsumable(ConsumableType.Book);
        public void SetItemToPotions() => AddConsumable(ConsumableType.Potion);

        public void SetItemToIron() => AddCurrency(CurrencyType.Iron);
        public void SetItemToCopper() => AddCurrency(CurrencyType.Copper);
        public void SetItemToSilver() => AddCurrency(CurrencyType.Silver);
        public void SetItemToGold() => AddCurrency(CurrencyType.Gold);

        public void ToggleAutoEquip() => Equipment.autoEquip = !Equipment.autoEquip;
        public void SortPlayerInventory() => Inventory.Sort();
        public void SortPlayerStash() => Stash.Sort();

        public void ClearPlayerEquipment() => RemoveAllItems(Equipment);
        public void ClearPlayerInventory() => RemoveAllItems(Inventory);
        public void ClearPlayerStash() => RemoveAllItems(Stash);
        public void RestockStore()
        {
            RemoveAllItems(Store);

            for (var i = 0; i < 20; i++)
            {
                var item = ItemProvider.Instance.GenerateRandomEquipment();

                _ = Store?.AddToContainer(new Package(null, item, 1));
            }
            Store.Sort();
        }

        // TODO: protect item losss when stash is full
        public void StashInventory()
        {
            var storedPackages = Inventory?.StoredPackages.ToList();

            for (var i = 0; i < storedPackages.Count; i++)
            {
                var package = Stash?.AddToContainer(storedPackages[i].Value);

                _ = package.Value.Item == null || package.Value.Amount <= 0
                    ? Inventory?.RemoveAtPosition(storedPackages[i].Key, storedPackages[i].Value)
                    : Inventory?.RemoveAtPosition(storedPackages[i].Key, package.Value);
            }
        }
    }
}
