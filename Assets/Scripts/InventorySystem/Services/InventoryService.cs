using Submodules.Utility.Services;
using System;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The <see cref="IInventoryService"/> of one boot, over a <see cref="ISession"/> and the
    /// <see cref="IItemService"/> that rolls a Restock's stock. The item service holds no hero, so
    /// the Hero's magic find is handed to every roll from here.
    /// </summary>
    public sealed class InventoryService : IInventoryService
    {
        private const int SupplyStock = 20;

        private readonly ISession session;
        private readonly IItemService items;

        /// <summary>Call sites at the Unity edge read the service as <c>InventoryService.Instance.RestockTownStops()</c>.</summary>
        public static IInventoryService Instance => ServiceLocator.Get<IInventoryService>();

        public InventoryService(ISession session, IItemService items)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public AbstractDimensionalContainer ContainerFor(ContainerRole role)
        {
            var hero = session.Hero;
            var world = session.World;

            return ContainerRoleResolver.Resolve(role, hero.Equipment, hero.Inventory, hero.Stash,
                world.VendorSupply, world.HealerSupply, world.Sold);
        }

        public QuickMoveIntent QuickMoveFor(AbstractDimensionalContainer source)
        {
            var hero = session.Hero;
            var world = session.World;

            return QuickMoveResolver.Resolve(world.Context.Active, source, hero.Inventory, hero.Stash, hero.Equipment,
                world.VendorSupply, world.HealerSupply, world.Sold);
        }

        public bool PickUpOrStash(Package package)
        {
            var hero = session.Hero;

            if (ItemAcquisition.TryPlace(ref package, hero.Equipment, hero.Inventory))
                return true;

            // Debug: try to add the remaining package amount to the Stash.
            if (Debug.isDebugBuild)
            {
                Debug.LogWarning($"Trying to add the remaining amount of {package.Amount} to the Stash");

                return hero.Stash.TryAddToContainer(ref package);
            }

            return false;
        }

        public void RestockVendorSupply()
        {
            session.World.Sold.RemoveAll();
            Fill(session.World.VendorSupply, () => items.RollEquipment(MagicFind));
        }

        public void RestockHealerSupply()
        {
            session.World.Sold.RemoveAll();
            Fill(session.World.HealerSupply, () => items.RollConsumable(MagicFind));
        }

        public void RestockTownStops()
        {
            session.World.Sold.RemoveAll();
            Fill(session.World.VendorSupply, () => items.RollEquipment(MagicFind));
            Fill(session.World.HealerSupply, () => items.RollConsumable(MagicFind));
        }

        // A hero without the stat (a template that never authored it) rolls at no bonus.
        public float MagicFind => session.Hero.GetStat(StatName.IncreasedItemRarity)?.TotalValue ?? 0f;

        public float ItemQuantity => session.Hero.GetStat(StatName.IncreasedItemQuantity)?.TotalValue ?? 0f;

        private static void Fill(AbstractDimensionalContainer supply, Func<ItemInstance> roll)
        {
            supply.RemoveAll();

            for (var i = 0; i < SupplyStock; i++)
            {
                var package = new Package(null, roll(), 1u);
                _ = supply.TryAddToContainer(ref package);
            }

            supply.Sort();
        }
    }
}
