using System;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// What the player does with the World's <see cref="GroundContainer"/>, with or without a Run: lay a
    /// Package on it, take one off it. Items go through the player's acquisition entry point
    /// (<see cref="GroundContainer.PickUp"/>); coins bank to the Wallet instead, and a coin stack pushed
    /// out by newer loot banks first. A Run's <see cref="LootFlow"/> works through one of these for its
    /// own ground, and the World keeps one for Town, where there is no loot flow.
    /// </summary>
    public sealed class GroundDesk : ILootGround
    {
        private readonly IItemReceiver _player;
        private readonly Wallet _wallet;

        public GroundDesk(GroundContainer ground, IItemReceiver player, Wallet wallet)
        {
            Ground = ground ?? throw new ArgumentNullException(nameof(ground));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        }

        public GroundContainer Ground { get; }

        /// <summary>
        /// Raised with the base-unit total of the coins each time some bank to the Wallet - a clicked stack,
        /// a stack pushed out by newer loot, or whatever <see cref="Bank"/> is asked to.
        /// </summary>
        internal event Action<long> CoinsBanked;

        /// <summary>Raised when the player's pick-up threw for a Drop (it stays on the ground).</summary>
        internal event Action<ItemInstance, Exception> PlacementFailed;

        /// <summary>Lays <paramref name="package"/> on the ground; a coin stack pushed out banks first.</summary>
        public bool PlaceOnGround(Package package) => Ground.TryLand(package, BankEvicted);

        /// <summary>
        /// The player picks the Package of <paramref name="item"/> up: coins bank to the Wallet, anything
        /// else goes through the acquisition entry point. What the Wallet or the bag cannot take stays on
        /// the ground. The Package is found by reference: the one clicked is the one that goes.
        /// </summary>
        /// <returns>Whether the player took any of it. False also for an item no longer on the ground.</returns>
        public bool PickUpFromGround(ItemInstance item)
        {
            if (!Ground.TryFind(item, out var cell, out var stored))
                return false;

            if (TryCoinOf(stored, out var coin))
            {
                var taken = Bank(coin, stored.Amount);
                if (taken == 0u)
                    return false;

                _ = Ground.RemoveAtPosition(cell, new Package(Ground, stored.Item, taken));
                _wallet.RelayIfCoin(stored.Item);
                return true;
            }

            return Ground.PickUp(item, _player, (failed, exception) => PlacementFailed?.Invoke(failed, exception));
        }

        /// <summary>
        /// Deposits <paramref name="amount"/> coins to the Wallet as far as it fits; what it has no cell for
        /// stays with the caller.
        /// </summary>
        /// <returns>How many coins the Wallet took.</returns>
        internal uint Bank(CurrencyType type, uint amount)
        {
            var taken = amount - _wallet.Deposit(Currency.Of(type, amount)).CountOf(type);

            if (0u < taken)
                CoinsBanked?.Invoke(checked((long)taken * Currency.ValueOf(type))); // the stack's value in iron base units (GLOSSARY.md "Base Unit")

            return taken;
        }

        private void BankEvicted(Package evicted)
        {
            if (TryCoinOf(evicted, out var type))
                _ = Bank(type, evicted.Amount);
        }

        private bool TryCoinOf(Package package, out CurrencyType type)
        {
            var view = Ground.ViewOf(package.Item);
            type = view.Definition.CurrencyType;
            return view.IsCurrency;
        }
    }
}
