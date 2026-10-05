using Submodules.Utility.Provider;
using System;
using System.Linq;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.GUI.InventoryDisplays;
using ToolSmiths.InventorySystem.Runtime.Provider;
using ToolSmiths.InventorySystem.Runtime.Simulation;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The scene's face of the inventory, a thin facade over the Hero and the World the Session
    /// was booted with (issue #112, ADR-0015): the containers, the Wallet and the Inventory
    /// Context are the Hero's and the World's, acquisition and Restock are
    /// <see cref="IInventoryService"/>'s. What stays here is what needs a scene object - the
    /// debug buttons' <c>UnityEvent</c> targets (#118 moves them), the Field face registration and
    /// the debug flags. Every member reads the current Hero and World on each call and caches
    /// neither, so the swap a hero load brings (#114) needs no change here.
    /// </summary>
    internal sealed class InventoryProvider : AbstractProvider<InventoryProvider>
    {
        // TODO: move player related inventories into the local player?
        public CharacterEquipment Equipment => Session.Instance.Hero.Equipment;
        public CharacterInventory Inventory => Session.Instance.Hero.Inventory;
        public CharacterInventory Stash => Session.Instance.Hero.Stash;

        /// <summary>The Vendor's Supply (the World's <see cref="World.VendorSupply"/>).</summary>
        public CharacterInventory Store => Session.Instance.World.VendorSupply;

        /// <summary>The Healer's Supply (issue #121): the second Supply after the Vendor's
        /// <see cref="Store"/>, stocked with consumables and bought from the same way. Sized like
        /// the Store - one size for every Supply shelf.</summary>
        public CharacterInventory HealerSupply => Session.Instance.World.HealerSupply;

        /// <summary>The Sold container (issue #126): what the player sold, bought back like a
        /// Supply, sold into by <see cref="Sale"/>, shown on each selling panel's Sold tab, and
        /// known to <see cref="QuickMoveFor"/> as a shelf.</summary>
        public SoldContainer Sold => Session.Instance.World.Sold;

        /// <summary>The player's spendable money, backed by <see cref="Inventory"/>'s coin
        /// cells. The wallet, not the container, owns currency logic since issue #14.</summary>
        public Wallet Wallet => Session.Instance.Hero.Wallet;

        /// <summary>The Inventory Context: which panels are up and where a Quick Move lands.
        /// The rule itself is the engine-free <see cref="InventoryContextState"/>, owned by the
        /// World; the provider only carries it to the scene, and every panel and toggle
        /// subscribes to it here.</summary>
        private static InventoryContextState ContextState => Session.Instance.World.Context;

        public InventoryContext ActiveContext => ContextState.Active;

        public event Action<InventoryContext> OnContextChanged
        {
            add => ContextState.Changed += value;
            remove => ContextState.Changed -= value;
        }

        /// <summary>Requests <paramref name="context"/> (issue #84) - the entry-point side of
        /// <see cref="InventoryContextState.Set"/>.</summary>
        public void SetContext(InventoryContext context) => ContextState.Set(context);

        /// <summary>Closes whatever context is active - always <see cref="InventoryContext.None"/>,
        /// never a per-context clear (see <see cref="InventoryContextState.Close"/>).</summary>
        public void CloseContext() => ContextState.Close();

        /// <summary>Drops the active context to <see cref="InventoryContext.None"/> if the Run
        /// phase just made it unreachable (see <see cref="InventoryContextState.SyncToPhase"/>) -
        /// the Send/Recall/Death/Go-Venture side of phase reachability (#84).</summary>
        public void SyncContextToPhase(bool inField) => ContextState.SyncToPhase(inField);

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

        [SerializeField] private Slider amountSlider;
        [SerializeField] private TextMeshProUGUI amountText;
        private uint Amount => amountSlider != null ? (uint)amountSlider.value : 1;

        /// <summary>
        /// The container-display registration seam (see <see cref="ContainerRole"/>): a display
        /// resolves its own container by role instead of this provider holding a hard scene
        /// reference to every display it owns - the direction that broke once the Vendor's slot
        /// grids started spawning at runtime instead of being hand-placed. Subscribed from
        /// <see cref="AbstractContainerDisplay.OnEnable"/>; mirrors
        /// <see cref="TrySubscribeContextChanged"/>'s guard, so a display enabling at edit time is
        /// left unbound (issue #46). The container comes from the inventory service, so no
        /// provider object has to exist in the scene (#112).
        /// </summary>
        /// <returns>Whether <paramref name="display"/> was actually bound.</returns>
        public static bool TryRegisterDisplay(AbstractContainerDisplay display, ContainerRole role)
        {
            if (!Application.isPlaying)
                return false;

            var container = InventoryService.Instance.ContainerFor(role);
            if (container == null)
                return false;

            display.SetupDisplay(container);
            return true;
        }

        /// <summary>
        /// Where a shift-click on <paramref name="source"/> should send its item, given the
        /// Inventory Context active right now (#30). The four containers and the context are all
        /// behind the inventory service, so a caller that holds one slot's container can ask with
        /// just that - rather than assembling the same six arguments at every slot display, which
        /// is how <c>VendorSlotDisplay</c> came to pass its own container as both the source
        /// and the shelf. <see cref="QuickMoveResolver"/> keeps the matrix and stays
        /// directly tested; this is only the seam callers hold.
        /// </summary>
        public QuickMoveIntent QuickMoveFor(AbstractDimensionalContainer source) =>
            InventoryService.Instance.QuickMoveFor(source);

        // The item service holds no hero: the debug rolls and the Restock hand it the player's bonuses.
        private static float MagicFind => InventoryService.Instance.MagicFind;
        private static float ItemQuantity => InventoryService.Instance.ItemQuantity;

        private void AddEquipment(EquipmentType equipmentType)
        {
            for (var i = 0; i < Amount; i++)
            {
                var randomEquipment = ItemService.Instance.RollEquipment(equipmentType, MagicFind);
                _ = InventoryService.Instance.PickUpOrStash(new Package(null, randomEquipment, 1u));
            }
        }

        private void AddConsumable(ConsumableType consumableType)
        {
            for (var i = 0; i < Amount; i++)
            {
                var randomConsumable = ItemService.Instance.RollConsumable(consumableType, MagicFind);
                _ = InventoryService.Instance.PickUpOrStash(new Package(null, randomConsumable, 1u));
            }
        }

        public void AddRandomLoot()
        {
            var loot = ItemService.Instance.RollLoot(Amount, MagicFind, ItemQuantity);

            for (var i = 0; i < loot.Count; i++)
                _ = InventoryService.Instance.PickUpOrStash(loot[i]);
        }

        public void AddRandomCurrency()
        {
            for (var i = 0; i < Amount; i++)
                _ = InventoryService.Instance.PickUpOrStash(ItemService.Instance.RollCurrency());
        }

        public void RemoveAllItems(AbstractDimensionalContainer container) => container?.RemoveAll();

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

        /// <summary>Both Town Stops' Restock, run when a Run is Recalled: the Vendor's shelf, the
        /// Healer's shelf and the Sold container, which is emptied once for both.</summary>
        public void RestockTownStops() => InventoryService.Instance.RestockTownStops();

        /// <summary>A Supply's Restock clears the Sold container at the moment it refills (issue
        /// #128): what was sold is stock like any other, so either Town Stop's Restock empties it.</summary>
        public void RestockStore() => InventoryService.Instance.RestockVendorSupply();

        /// <summary>The Healer Supply's Restock (issue #121): the same refill as
        /// <see cref="RestockStore"/>, from rolled consumables instead of equipment.</summary>
        public void RestockHealerSupply() => InventoryService.Instance.RestockHealerSupply();

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
