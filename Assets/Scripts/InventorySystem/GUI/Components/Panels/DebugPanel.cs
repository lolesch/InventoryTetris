using System.Linq;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Components.Panels
{
    /// <summary>
    /// The dev tools' one view (issue #118): the debug spawn, clear, sort, kill, heal and
    /// amount-slider controls. Each public method is a <c>UnityEvent</c> target in a scene and
    /// does its work through a service (<see cref="IItemService"/>, <see cref="IInventoryService"/>,
    /// <see cref="ISession"/>) read on every call, so nothing here caches the Hero or the World
    /// across a hero load (#114) and no state class has to be a scene component.
    /// </summary>
    public sealed class DebugPanel : MonoBehaviour
    {
        [SerializeField] private Slider amountSlider;
        [SerializeField] private TextMeshProUGUI amountText;

        private uint Amount => amountSlider != null ? (uint)amountSlider.value : 1;

        public void SetAmountText()
        {
            if (amountText != null && amountSlider != null)
                amountText.text = amountSlider.value.ToString();
        }

        // The item service holds no hero: the debug rolls take the player's bonuses from the inventory service.
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

        public void ToggleAutoEquip()
        {
            var equipment = Session.Instance.Hero.Equipment;
            equipment.autoEquip = !equipment.autoEquip;
        }

        public void SortPlayerInventory() => Session.Instance.Hero.Inventory.Sort();
        public void SortPlayerStash() => Session.Instance.Hero.Stash.Sort();
        public void ConsolidatePlayerCurrency() => Session.Instance.Hero.Wallet.Consolidate();

        public void ClearPlayerEquipment() => Session.Instance.Hero.Equipment.RemoveAll();
        public void ClearPlayerInventory() => Session.Instance.Hero.Inventory.RemoveAll();
        public void ClearPlayerStash() => Session.Instance.Hero.Stash.RemoveAll();

        /// <summary>A Supply's Restock clears the Sold container at the moment it refills (issue
        /// #128): what was sold is stock like any other.</summary>
        public void RestockStore() => InventoryService.Instance.RestockVendorSupply();

        /// <summary>The Healer Supply's Restock (issue #121): the same refill, from rolled consumables.</summary>
        public void RestockHealerSupply() => InventoryService.Instance.RestockHealerSupply();

        public void StashInventory()
        {
            var hero = Session.Instance.Hero;
            var storedPackages = hero.Inventory.StoredPackages.ToList();

            for (var i = 0; i < storedPackages.Count; i++)
            {
                var package = storedPackages[i].Value;

                if (hero.Stash.TryAddToContainer(ref package))
                {
                    _ = package.Item == null || package.Amount <= 0
                        ? hero.Inventory.RemoveAtPosition(storedPackages[i].Key, storedPackages[i].Value)
                        : hero.Inventory.RemoveAtPosition(storedPackages[i].Key, package);
                }
            }
        }

        public void KillPlayer() => Session.Instance.Hero.GetResource(StatName.Health).DepleteCurrent();

        /// <summary>A full Health and Resource refill, the Healer's.</summary>
        public void HealPlayer() => Session.Instance.Hero.Heal();

        public void ToggleSpendingResource()
        {
            var hero = Session.Instance.Hero;
            hero.SpendResource = !hero.SpendResource;
        }
    }
}
