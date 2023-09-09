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
        [field: SerializeField] public PlayerInventory PlayerInventory { get; private set; }
        [field: SerializeField] public PlayerInventory PlayerStash { get; private set; }
        [field: SerializeField] public CharacterEquipment PlayerEquipment { get; private set; }
        [field: SerializeField] public AbstractDimensionalContainer ContainerToAddTo { get; private set; }

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

        [SerializeField, ReadOnly] private bool add = true;

        [SerializeField] private Slider amountSlider;
        [SerializeField] private TextMeshProUGUI amountText;
        private uint Amount => amountSlider != null ? (uint)amountSlider.value : 1;

        private void SetInventories()
        {
            PlayerInventoryDisplay.SetupDisplay(PlayerInventory);
            PlayerStashDisplay.SetupDisplay(PlayerStash);
            PlayerEquipmentDisplay.SetupDisplay(PlayerEquipment);
        }

        [ContextMenu("Awake")]
        public void Awake()
        {
            //amountSlider = GetComponentInChildren<Slider>();
            //amountText = amountSlider?.GetComponentInChildren<TextMeshProUGUI>();

            /// serialize inventories
            PlayerInventory = new(playerInventorySize);
            PlayerStash = new(playerStashSize);
            PlayerEquipment = new(playerEquipmentSize);

            ContainerToAddTo = PlayerInventory; // should get the current active toggle instead
            add = true;

            SetInventories();
        }

        private void AddRemoveEquipment(EquipmentType equipmentType)
        {
            for (var i = 0; i < Amount; i++)
            {
                if (add)
                {
                    var randomEquipment = ItemProvider.Instance.GenerateRandomOfEquipmentType(equipmentType);
                    _ = ContainerToAddTo?.AddToContainer(new Package(null, randomEquipment, 1));
                }
                else
                {
                    // TODO: remove Item of type {equipmentType}
                }
            }
        }

        private void AddRemoveConsumable(ConsumableType consumableType)
        {
            for (var i = 0; i < Amount; i++)
            {
                if (add)
                {
                    var randomConsumable = ItemProvider.Instance.GenerateRandomOfConsumableType(consumableType);
                    _ = ContainerToAddTo?.AddToContainer(new Package(null, randomConsumable, 1));
                }
                else
                {
                    // TODO: remove Item of type {consumableType}
                }
            }
        }

        public void AddRemoveRandomLoot()
        {
            if (add)
            {
                var items = ItemProvider.Instance.GenerateRandomLoot(Amount);

                for (var i = 0; i < items.Count; i++)
                    _ = ContainerToAddTo?.AddToContainer(new Package(null, items[i], 1));
            }
            else // needs testing
            {
                var storedPackages = ContainerToAddTo?.StoredPackages.ToList();
                var amountToRemove = Amount;

                for (var i = storedPackages.Count; i-- > 0;)
                {
                    var removedPackage = ContainerToAddTo.RemoveAtPosition(storedPackages[i].Key, storedPackages[i].Value);

                    amountToRemove -= removedPackage.Amount;

                    if (amountToRemove <= 0)
                        break;
                }
            }
        }

        [ContextMenu("RemoveAllItems")]
        public void RemoveAllItems() => RemoveAllItems(ContainerToAddTo);
        public void RemoveAllItems(AbstractDimensionalContainer container)
        {
            var storedPackages = container?.StoredPackages.ToList();
            for (var i = 0; i < storedPackages.Count; i++)
                _ = container.RemoveAtPosition(storedPackages[i].Key, storedPackages[i].Value);
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

        public void SetItemToArrows() => AddRemoveConsumable(ConsumableType.Arrows);
        public void SetItemToBooks() => AddRemoveConsumable(ConsumableType.Books);
        public void SetItemToPotions() => AddRemoveConsumable(ConsumableType.Potions);

        public void ToggleAddRemove() => add = !add;

        public void ToggleAutoEquip() => PlayerEquipment.autoEquip = !PlayerEquipment.autoEquip;

        //public void SortSelectedInventory() => ContainerToAddTo.Sort();
        public void SortPlayerInventory() => PlayerInventory.Sort();
        public void SortPlayerStash() => PlayerStash.Sort();

        public void ClearPlayerInventory() => RemoveAllItems(PlayerInventory);
        public void ClearPlayerStash() => RemoveAllItems(PlayerStash);
        public void ClearPlayerEquipment() => RemoveAllItems(PlayerEquipment);

        //public Package EquipItem(Package package) => PlayerEquipment.AddToContainer(package);
    }
}
