using Submodules.Utility.Provider;
using System;
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
    internal sealed class InventoryProvider : AbstractProvider<InventoryProvider>
    {
        // TODO: move player related inventories into the local player?
        [field: SerializeField] public CharacterEquipment Equipment { get; private set; }
        [field: SerializeField] public CharacterInventory Inventory { get; private set; }
        [field: SerializeField] public CharacterInventory Stash { get; private set; }
        [field: SerializeField] public CharacterInventory Store { get; private set; }

        /// <summary>The Sell Basket (issue #32/#33) - the grid the player stages a sale in,
        /// with the origin ledger a Cancel uses to return each Package. Owned here with the
        /// other player containers so a shift-click quick-move can both target it (backpack /
        /// equipment → basket) and recognise it as a source (basket → backpack).</summary>
        public SellBasket.Basket Basket { get; private set; }

        /// <summary>The player's spendable money, backed by <see cref="Inventory"/>'s coin
        /// cells. The wallet, not the container, owns currency logic since issue #14.</summary>
        public Wallet Wallet { get; private set; }

        /// <summary>The Inventory Context: which panels are up and where a Quick Move lands.
        /// The rule itself is the engine-free <see cref="InventoryContextState"/>; the provider
        /// only carries it to the scene, and every panel and toggle subscribes to it here.</summary>
        private readonly InventoryContextState inventoryContext = new();

        public InventoryContext ActiveContext => inventoryContext.Active;

        public event Action<InventoryContext> OnContextChanged
        {
            add => inventoryContext.Changed += value;
            remove => inventoryContext.Changed -= value;
        }

        /// <summary>Requests <paramref name="context"/> (issue #84) - the entry-point side of
        /// <see cref="InventoryContextState.Set"/>.</summary>
        public void SetContext(InventoryContext context) => inventoryContext.Set(context);

        /// <summary>Closes whatever context is active - always <see cref="InventoryContext.None"/>,
        /// never a per-context clear (see <see cref="InventoryContextState.Close"/>).</summary>
        public void CloseContext() => inventoryContext.Close();

        /// <summary>Drops the active context to <see cref="InventoryContext.None"/> if the Run
        /// phase just made it unreachable (see <see cref="InventoryContextState.SyncToPhase"/>) -
        /// the Send/Recall/Death/Go-Venture side of phase reachability (#84).</summary>
        public void SyncContextToPhase(bool inField) => inventoryContext.SyncToPhase(inField);

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

        /// <summary>The Sell Basket's grid (issue #66) - a new grid alongside the Supply
        /// shelf (<see cref="StoreDisplay"/>), wired the same way <see cref="StashDisplay"/>
        /// and <see cref="InventoryDisplay"/> are, and bound by <see cref="SellBasketDisplay"/>.</summary>
        [Space]
        public InventoryContainerDisplay BasketDisplay;
        [SerializeField] private Vector2Int basketSize = new(5, 3);

        [SerializeField] private Slider amountSlider;
        [SerializeField] private TextMeshProUGUI amountText;
        private uint Amount => amountSlider != null ? (uint)amountSlider.value : 1;

        private void SetInventories()
        {
            EquipmentDisplay.SetupDisplay(Equipment);
            InventoryDisplay.SetupDisplay(Inventory);
            StashDisplay.SetupDisplay(Stash);

            StoreDisplay.SetupDisplay(Store);
            if (BasketDisplay != null)
                BasketDisplay.SetupDisplay(Basket.Container);
        }

        /// <summary>
        /// Where a shift-click on <paramref name="source"/> should send its item, given the
        /// Inventory Context active right now (#30). The four containers and the context are all
        /// here, so a caller that holds one slot's container can ask with just that -
        /// rather than assembling the same six arguments at every slot display, which is
        /// how <c>VendorSlotDisplay</c> came to pass its own container as both the source
        /// and the shelf. <see cref="QuickMoveResolver"/> keeps the matrix and stays
        /// directly tested; this is only the seam callers hold.
        /// </summary>
        public QuickMoveIntent QuickMoveFor(AbstractDimensionalContainer source) =>
            QuickMoveResolver.Resolve(ActiveContext, source, Inventory, Stash, Equipment, Store, Basket.Container);

        public void Awake()
        {
            /// The container core lives in InventorySystem.Containers and names no
            /// provider - it takes the character and the coin minter as interfaces here,
            /// where the four containers are newed up. The drag cursor is wrapped per-move
            /// as a CursorHolder by the slot displays, so the containers no longer hold one.
            var statReceiver = CharacterProvider.Instance.Player;
            var currencyMinter = ItemProvider.Instance;

            Equipment = new(equipmentSize, statReceiver);
            Inventory = new(inventorySize);
            Stash = new(stashSize);
            Store = new(storeSize);
            Basket = new SellBasket.Basket(new(basketSize));

            Wallet = new Wallet(Inventory, currencyMinter);

            RestockStore();

            SetInventories();
        }

        private void AddEquipment(EquipmentType equipmentType)
        {
            for (var i = 0; i < Amount; i++)
            {
                var randomEquipment = ItemProvider.Instance.RollEquipment(equipmentType);
                _ = CharacterProvider.Instance.Player.PickUpItem(new Package(null, randomEquipment, 1u));
            }
        }

        private void AddConsumable(ConsumableType consumableType)
        {
            for (var i = 0; i < Amount; i++)
            {
                var randomConsumable = ItemProvider.Instance.RollConsumable(consumableType);
                _ = CharacterProvider.Instance.Player.PickUpItem(new Package(null, randomConsumable, 1u));
            }
        }

        public void AddRandomLoot()
        {
            var loot = ItemProvider.Instance.RollLoot(Amount);

            for (var i = 0; i < loot.Count; i++)
                _ = CharacterProvider.Instance.Player.PickUpItem(loot[i]);
        }

        public void AddRandomCurrency()
        {
            for (var i = 0; i < Amount; i++)
                _ = CharacterProvider.Instance.Player.PickUpItem(ItemProvider.Instance.RollCurrency());
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

        public void ToggleAutoEquip() => Equipment.autoEquip = !Equipment.autoEquip;
        public void SortPlayerInventory() => Inventory.Sort();
        public void ConsolidatePlayerCurrency() => Wallet.Consolidate();
        public void SortPlayerStash() => Stash.Sort();

        public void ClearPlayerEquipment() => RemoveAllItems(Equipment);
        public void ClearPlayerInventory() => RemoveAllItems(Inventory);
        public void ClearPlayerStash() => RemoveAllItems(Stash);
        public void RestockStore()
        {
            RemoveAllItems(Store);

            for (var i = 0; i < 20; i++)
            {
                var item = ItemProvider.Instance.RollEquipment();

                var package = new Package(null, item, 1u);

                _ = Store?.TryAddToContainer(ref package);
            }
            Store.Sort();
        }

        public void StashInventory()
        {
            var storedPackages = Inventory?.StoredPackages.ToList();

            for (var i = 0; i < storedPackages.Count; i++)
            {
                var package = storedPackages[i].Value;

                if (Stash.TryAddToContainer(ref package))
                {
                    _ = package.Item == null || package.Amount <= 0
                        ? Inventory?.RemoveAtPosition(storedPackages[i].Key, storedPackages[i].Value)
                        : Inventory?.RemoveAtPosition(storedPackages[i].Key, package);
                }
            }
        }
    }
}
