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

        /// <summary>
        /// Detach-before-attach subscribe to <see cref="IInventoryService.ContextChanged"/> for a view that
        /// tracks the Inventory Context. The event follows whichever World is current, so one subscription
        /// outlives a hero load. Does nothing, and says so, when no service is armed - an enable in Edit
        /// Mode - rather than throwing from the locator.
        /// </summary>
        /// <returns>Whether the subscription was made; <paramref name="activeContext"/> is only meaningful when it was.</returns>
        public static bool TrySubscribeContextChanged(Action<InventoryContext> handler, out InventoryContext activeContext)
        {
            activeContext = default;

            if (!ServiceLocator.IsArmed)
                return false;

            var service = Instance;
            service.ContextChanged -= handler;
            service.ContextChanged += handler;

            activeContext = service.ActiveContext;
            return true;
        }

        /// <summary>The matching detach for <see cref="TrySubscribeContextChanged"/>, tolerant of nothing being armed.</summary>
        public static void UnsubscribeContextChanged(Action<InventoryContext> handler)
        {
            if (ServiceLocator.IsArmed)
                Instance.ContextChanged -= handler;
        }

        // The World the relay is attached to, so a hero load can let go of exactly that one.
        private World attached;

        public InventoryService(ISession session, IItemService items)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.items = items ?? throw new ArgumentNullException(nameof(items));

            Attach(session.World);
            session.HeroLoaded += OnHeroLoaded;
        }

        public event Action<InventoryContext> ContextChanged;

        public InventoryContext ActiveContext => session.World.Context.Active;

        public void SetContext(InventoryContext context) => session.World.Context.Set(context);

        public void CloseContext() => session.World.Context.Close();

        public void SyncContextToPhase(bool inField) => session.World.Context.SyncToPhase(inField);

        private void Attach(World world)
        {
            attached = world;
            world.Context.Changed += RelayContext;
        }

        private void RelayContext(InventoryContext context) => ContextChanged?.Invoke(context);

        // A new World is a new context and new shelves. The relay moves to it, so a subscriber of
        // ContextChanged stays subscribed through the swap, and the shelves are stocked as a booted
        // World's are. Subscribers are told once what the new World starts at (closed), so a panel
        // that was up for the old World comes down.
        private void OnHeroLoaded()
        {
            attached.Context.Changed -= RelayContext;
            Attach(session.World);

            RestockTownStops();

            ContextChanged?.Invoke(session.World.Context.Active);
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

            // The ground exists exactly while a Run's loot flow does (issue #63): Town has none.
            return QuickMoveResolver.Resolve(world.Context.Active, source, hero.Inventory, hero.Stash, hero.Equipment,
                world.VendorSupply, world.HealerSupply, world.Sold, groundOpen: world.LootFlow != null);
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
