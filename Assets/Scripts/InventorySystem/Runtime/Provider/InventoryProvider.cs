using Submodules.Utility.Provider;
using System;
using System.Linq;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.GUI.InventoryDisplays;
using ToolSmiths.InventorySystem.Runtime.Provider;
using ToolSmiths.InventorySystem.Runtime.Simulation;
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

        /// <summary>The InFields face - unreachable while it is up (InField, and the Go Venture
        /// preview alike, since both show it). Not authored: registered by
        /// <see cref="RegisterFieldFacePanel"/>, the same seam as <see cref="TryRegisterDisplay"/>
        /// and for the same reason - a provider can be created fresh mid-run (see
        /// <see cref="AbstractProvider{T}"/>/<c>AbstractSceneSingleton&lt;T&gt;</c>), and a fresh
        /// instance has no Inspector-authored fields, so a hard scene reference here would
        /// silently go null and leave every Town Stop reading as always-reachable.</summary>
        private FieldFacePanel fieldFacePanel;

        /// <summary>The InFields face's own registration, mirroring <see cref="TryRegisterDisplay"/>:
        /// <see cref="FieldFacePanel"/> registers itself (from its own <c>OnEnable</c>) rather
        /// than this provider holding a hard reference to it.</summary>
        public static void RegisterFieldFacePanel(FieldFacePanel panel)
        {
            if (!Application.isPlaying)
                return;

            var provider = Instance;
            if (provider == null)
                return;

            provider.fieldFacePanel = panel;
        }

        /// <summary>Whether a Town Stop can be reached right now - false whenever
        /// <see cref="fieldFacePanel"/> is up, which <see cref="RunPhasePanel"/>'s shared
        /// <see cref="Submodules.Utility.UI.PanelGroup"/> keeps correct through every InTown/
        /// InField edge, deliberate (the buttons) or not (Death). <see cref="SidePanelToggle"/>
        /// asks this instead of each holding its own reference to the panel.</summary>
        public bool IsFieldReachable => fieldFacePanel == null || !fieldFacePanel.IsVisible;

        /// <summary>
        /// Detach-before-attach <see cref="OnContextChanged"/> subscribe, shared by every
        /// panel/toggle/provider that tracks the Inventory Context (issue #85) - four
        /// independent copies of this same guard-and-resubscribe idiom collapse into one.
        /// No-ops outside Play mode, or before a provider exists, so a caller mid-domain-reload
        /// or edit-time enable is left unsubscribed rather than creating one (issue #46).
        /// </summary>
        /// <returns>Whether the subscription was actually made; <paramref name="activeContext"/>
        /// is only meaningful when it was.</returns>
        public static bool TrySubscribeContextChanged(Action<InventoryContext> handler, out InventoryContext activeContext)
        {
            activeContext = default;

            if (!Application.isPlaying)
                return false;

            var provider = Instance;
            if (provider == null)
                return false;

            provider.OnContextChanged -= handler;
            provider.OnContextChanged += handler;

            activeContext = provider.ActiveContext;
            return true;
        }

        /// <summary>The matching detach for <see cref="TrySubscribeContextChanged"/>, tolerant of
        /// a provider that no longer exists (scene teardown) or a handler never subscribed.</summary>
        public static void UnsubscribeContextChanged(Action<InventoryContext> handler)
        {
            if (Application.isPlaying && Instance != null)
                Instance.OnContextChanged -= handler;
        }

        [field: SerializeField] public bool ShowDebugPositions { get; private set; }

        [Space]
        [SerializeField] private Vector2Int equipmentSize = new(14, 1);
        [SerializeField] private Vector2Int inventorySize = new(10, 6);
        [SerializeField] private Vector2Int stashSize = new(10, 16);
        [SerializeField] private Vector2Int storeSize = new(10, 16);

        /// <summary>The Sell Basket's grid size (issue #66) - the basket alongside the Supply
        /// shelf (<see cref="Store"/>), sized the same way <see cref="Stash"/> and
        /// <see cref="Inventory"/> are, and bound by <see cref="SellBasketDisplay"/>.</summary>
        [SerializeField] private Vector2Int basketSize = new(5, 3);

        [SerializeField] private Slider amountSlider;
        [SerializeField] private TextMeshProUGUI amountText;
        private uint Amount => amountSlider != null ? (uint)amountSlider.value : 1;

        /// <summary>
        /// The container-display registration seam (see <see cref="ContainerRole"/>): a display
        /// resolves its own container by role instead of this provider holding a hard scene
        /// reference to every display it owns - the direction that broke once the Vendor's slot
        /// grids started spawning at runtime instead of being hand-placed. Subscribed from
        /// <see cref="AbstractContainerDisplay.OnEnable"/>; mirrors
        /// <see cref="TrySubscribeContextChanged"/>'s guard-and-resolve shape, so a display
        /// enabling mid-domain-reload or at edit time is left unbound rather than creating a
        /// provider (issue #46).
        /// </summary>
        /// <returns>Whether <paramref name="display"/> was actually bound.</returns>
        public static bool TryRegisterDisplay(AbstractContainerDisplay display, ContainerRole role)
        {
            if (!Application.isPlaying)
                return false;

            var provider = Instance;
            if (provider == null)
                return false;

            var container = provider.ContainerFor(role);
            if (container == null)
                return false;

            display.SetupDisplay(container);
            return true;
        }

        private AbstractDimensionalContainer ContainerFor(ContainerRole role) => role switch
        {
            ContainerRole.Equipment => Equipment,
            ContainerRole.Inventory => Inventory,
            ContainerRole.Stash => Stash,
            ContainerRole.Store => Store,
            ContainerRole.Basket => Basket?.Container,
            _ => null,
        };

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
