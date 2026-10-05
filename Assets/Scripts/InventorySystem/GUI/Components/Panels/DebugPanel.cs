using Submodules.Utility.Extensions;
using System.Linq;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Components.Panels
{
    /// <summary>
    /// The dev tools' one view (issue #118): the debug spawn, clear, sort, kill, heal and
    /// amount-slider controls. Each public method is a <c>UnityEvent</c> target in a scene and
    /// does its work through a service (<see cref="IItemService"/>, <see cref="IInventoryService"/>,
    /// <see cref="ISession"/>) read on every call, so no control caches the Hero or the World
    /// across a hero load (#114) and no state class has to be a scene component. The one
    /// exception is the dev log of the Hero's death and damage, which follows the Hero through
    /// <see cref="ISession.HeroLoaded"/>.
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

        public void ClearVendorSupply() => Session.Instance.World.VendorSupply.RemoveAll();
        public void ClearHealerSupply() => Session.Instance.World.HealerSupply.RemoveAll();
        public void ClearSold() => Session.Instance.World.Sold.RemoveAll();

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
                var (cell, stored) = (storedPackages[i].Key, storedPackages[i].Value);
                var package = stored;

                if (!hero.Stash.TryAddToContainer(ref package))
                    continue;

                // What the Stash took is what leaves the Inventory; a stack it only part-took keeps the rest.
                var moved = stored.Amount - package.Amount;

                if (0 < moved)
                    _ = hero.Inventory.RemoveAtPosition(cell, new Package(null, stored.Item, moved));
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

        /// <summary>A new Hero and World from the default hero template (#114); refused, with a
        /// warning, while a Run is in the Field.</summary>
        public void LoadDefaultHero() => LoadDefault();

        /// <summary>The one statement of the dev hero load, shared with the Editor menu.</summary>
        public static void LoadDefault()
        {
            if (Session.Instance.TryLoad(GameBoot.Load().DefaultHero))
                Debug.Log("Loaded a new Hero and World from the default hero.");
            else
                Debug.LogWarning("A Run is in the Field: Recall before loading a hero.");
        }

        // The Hero the dev log lines below are bound to: the death, depletion and damage reactions
        // the retired LocalPlayer logged. Let go on disable and swapped on a hero load (#114).
        private Hero _logged;

        private void OnEnable()
        {
            if (Session.TrySubscribeHeroLoaded(LogHero))
                LogHero();
        }

        private void OnDisable()
        {
            Session.UnsubscribeHeroLoaded(LogHero);

            StopLogging();
        }

        private void LogHero()
        {
            StopLogging();

            _logged = Session.Instance.Hero;

            // A template without a Health or Resource stat has nothing to react to.
            var health = _logged.GetResource(StatName.Health);
            if (health != null)
                health.CurrentHasDepleted += LogDeath;

            var resource = _logged.GetResource(StatName.Resource);
            if (resource != null)
                resource.CurrentHasDepleted += LogResourceDepleted;

            _logged.DamageDealt += LogDamageDealt;
            _logged.DamageReceived += LogDamageReceived;
        }

        private void StopLogging()
        {
            if (_logged == null)
                return;

            var health = _logged.GetResource(StatName.Health);
            if (health != null)
                health.CurrentHasDepleted -= LogDeath;

            var resource = _logged.GetResource(StatName.Resource);
            if (resource != null)
                resource.CurrentHasDepleted -= LogResourceDepleted;

            _logged.DamageDealt -= LogDamageDealt;
            _logged.DamageReceived -= LogDamageReceived;
            _logged = null;
        }

        private void LogDeath() => Debug.LogWarning($"Hero {"DIED!".Colored(Color.red)}", this);

        private void LogResourceDepleted() => Debug.LogWarning($"Hero resource {"depleted".Colored(Color.red)}", this);

        private void LogDamageDealt(DamageType damageType, float damageOutput) =>
            Debug.Log($"Hero deals {damageOutput.ToString().Colored(Color.red)} {damageType}", this);

        private void LogDamageReceived(DamageType damageType, float absorbed, float received)
        {
            Debug.Log($"Hero absorbs {absorbed.ToString().Colored(Color.red)} {damageType}", this);
            Debug.Log($"Hero receives {received.ToString().Colored(Color.red)} {damageType}", this);
        }
    }
}
