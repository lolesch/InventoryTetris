using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// The player's spendable money. The coins physically sit in a backing container's grid
    /// cells - that is their rendering - but the wallet, not the container, is the authority
    /// on <see cref="Balance"/>. Extracted from <see cref="CharacterInventory"/>
    /// (foundational-rework Phase 3, issue #14) so a <c>Store</c> and a <c>Stash</c> stop
    /// carrying currency machinery they never use.
    ///
    /// <para><see cref="TryPay"/> spends the smallest denominations first (the math is
    /// <see cref="Currency.TryGetPayment"/>) and banks any overpay straight back as change,
    /// so a pay-then-refund of the same value nets to zero. <see cref="Consolidate"/> folds
    /// loose coins upward and preserves total value. <see cref="Deposit"/> is the one
    /// coin-mint path - buy change, sale proceeds and the manual button all route through
    /// it.</para>
    /// </summary>
    public sealed class Wallet
    {
        private readonly AbstractDimensionalContainer coins;
        private readonly ICurrencyMinter minter;

        private Currency lastBalance;
        private bool isConsolidating;

        /// Depth of the wallet operation currently in flight. While it is non-zero the
        /// backing container fires <see cref="AbstractDimensionalContainer.OnContentChanged"/>
        /// once per coin package it touches; <see cref="OnBalanceChanged"/> is held back and
        /// raised once, with the settled balance, when the outermost operation returns.
        private int mutating;

        /// <param name="coins">The container whose currency packages are this wallet's
        /// coins - the player <see cref="CharacterInventory"/> in the running game.</param>
        /// <param name="minter">Mints the coins <see cref="Deposit"/> banks and the change
        /// <see cref="TryPay"/> hands back. Null only in a test that never deposits.</param>
        public Wallet(AbstractDimensionalContainer coins, ICurrencyMinter minter = null)
        {
            this.coins = coins ?? throw new ArgumentNullException(nameof(coins));
            this.minter = minter;

            lastBalance = CalculateCash();
            this.coins.OnContentChanged += OnCoinsChanged;
        }

        /// <summary>
        /// The container the coins live in. Enrol it in an <see cref="ItemTransaction"/> when
        /// a move's deposit or payment must land only on commit (<see cref="VendorTransaction"/>).
        /// </summary>
        public AbstractDimensionalContainer Container => coins;

        /// <summary>
        /// One coin of <paramref name="type"/>, minted the way <see cref="Deposit"/> mints, for a pile
        /// that lies somewhere else (the ground) before it banks. Null when the wallet cannot mint it.
        /// </summary>
        public ItemInstance MintCoin(CurrencyType type) => minter?.MintCurrency(type);

        /// <summary>Fires when <see cref="Balance"/> changes, however the coins moved - a
        /// deposit, a payment, or the player dragging a coin in or out of the grid.</summary>
        public event Action<Currency> OnBalanceChanged;

        /// <summary>The spendable total, summed fresh from the backing container's coins.</summary>
        public Currency Balance => CalculateCash();

        /// <summary>Whether the wallet holds at least <paramref name="price"/> in value.</summary>
        public bool CanAfford(Currency price) => price.Total <= CalculateCash().Total;

        /// <summary>
        /// Spends <paramref name="price"/>, smallest denomination first, banking any overpay
        /// back as change - so the wallet never loses more than the price. Returns false and
        /// spends nothing when the balance is short; a zero price succeeds for free.
        /// </summary>
        public bool TryPay(Currency price)
        {
            if (!CalculateCash().TryGetPayment(price, out var toRemove, out var change))
                return false;

            BeginMutation();
            try
            {
                RemoveCoins(toRemove);

                if (0u < change.Total)
                    Deposit(change);
            }
            finally
            {
                EndMutation();
            }

            return true;
        }

        /// <summary>
        /// Banks <paramref name="amount"/> as minted coins, largest denomination first, so a
        /// container short of cells keeps the highest. A denomination over its stack limit spills
        /// into further cells. Consolidates afterwards, so every coin that goes through the
        /// wallet - loot, change, sale proceeds - ends up folded into the largest denominations.
        /// </summary>
        /// <returns>The coins that found no cell; the caller decides where they go.</returns>
        public Currency Deposit(Currency amount)
        {
            var unplaced = default(Currency);

            BeginMutation();
            try
            {
                foreach (var type in Currency.Denominations)
                    unplaced = unplaced.With(type, AddCoins(type, amount.CountOf(type)));

                Consolidate();
            }
            finally
            {
                EndMutation();
            }

            return unplaced;

            uint AddCoins(CurrencyType type, uint count)
            {
                if (0u == count)
                    return 0u;

                var coin = minter?.MintCurrency(type);
                if (coin == null)
                    return count;

                var package = new Package(coins, coin, count);
                _ = coins.TryAddToContainer(ref package);
                return package.Amount;
            }
        }

        /// <summary>
        /// Folds every coin into the largest denominations that fit, leaving the remainder
        /// loose, and re-lays coins that sit out of order gold first, iron last. Value-preserving:
        /// <see cref="Balance"/> is the same before and after, so no
        /// <see cref="OnBalanceChanged"/> is raised - only the grid repaints. The re-entrancy
        /// guard is because <see cref="Deposit"/> consolidates and this re-enters it.
        /// </summary>
        public void Consolidate()
        {
            if (isConsolidating)
                return;

            var current = CalculateCash();

            if (0u == current.Total)
                return;

            var consolidated = new Currency(current.Total);

            if (consolidated.Iron == current.Iron
                && consolidated.Copper == current.Copper
                && consolidated.Silver == current.Silver
                && consolidated.Gold == current.Gold
                && CoinsLieHighestFirst())
                return; // already canonical and in order - don't churn the grid

            Lay(current, fold: true);
        }

        /// <summary>
        /// A Package the player just moved into the bag (a drop, a shift-click): when it is a coin,
        /// re-lays the bag's coins gold first, iron last, so a coin placed by hand ends up in order
        /// with the rest. Folds nothing - the coins the player holds stay the coins they hold - and
        /// leaves the coins alone for any other Package.
        /// </summary>
        public void RelayIfCoin(ItemInstance arrived)
        {
            if (!coins.ViewOf(arrived).IsCurrency || CoinsLieHighestFirst())
                return;

            Lay(CalculateCash(), fold: false);
        }

        /// <summary>
        /// <see cref="RelayIfCoin"/> for the destination a drag-drop landed in: only a coin that landed in
        /// the bag (<see cref="Container"/>) re-lays it, a coin dropped anywhere else stays where it fell.
        /// </summary>
        public void RelayIfCoinLandedIn(AbstractDimensionalContainer destination, ItemInstance arrived)
        {
            if (destination == coins)
                RelayIfCoin(arrived);
        }

        /// <summary>Takes <paramref name="current"/> out of the bag and lays it back largest first, folded upward when <paramref name="fold"/>.</summary>
        private void Lay(Currency current, bool fold)
        {
            isConsolidating = true;
            BeginMutation();

            try
            {
                RemoveCoins(current);

                var unplaced = Deposit(fold ? new Currency(current.Total) : current);

                // Folded coins can need more cells than the stacks they came from (999 iron is 199 copper
                // and 4 iron): when they do not fit, the coins go back as they were.
                if (0u < unplaced.Total)
                {
                    RemoveCoins(CalculateCash());
                    _ = Deposit(current);
                }
            }
            finally
            {
                isConsolidating = false;
                EndMutation();
            }

            coins.InvokeRefresh();
        }

        /// <summary>
        /// Whether no coin sits ahead of a more valuable one, in the order the container fills its cells
        /// (column by column) - the order <see cref="Deposit"/> lays them in.
        /// </summary>
        private bool CoinsLieHighestFirst()
        {
            var previous = uint.MaxValue;

            foreach (var entry in coins.StoredPackages.OrderBy(e => e.Key.x).ThenBy(e => e.Key.y))
            {
                var view = coins.ViewOf(entry.Value.Item);
                if (!view.IsCurrency)
                    continue;

                var value = Currency.ValueOf(view.Definition.CurrencyType);
                if (previous < value)
                    return false;

                previous = value;
            }

            return true;
        }

        private void BeginMutation() => mutating++;

        /// <summary>Closes one <see cref="BeginMutation"/>; the outermost close raises the
        /// held <see cref="OnBalanceChanged"/> once, with the settled balance.</summary>
        private void EndMutation()
        {
            if (--mutating <= 0)
                RaiseBalanceIfChanged();
        }

        /// <summary>
        /// Raises <see cref="OnBalanceChanged"/> when the spendable total has actually moved
        /// since it was last raised. The one place the event fires.
        /// </summary>
        private void RaiseBalanceIfChanged()
        {
            var balance = CalculateCash();

            if (balance.Total == lastBalance.Total)
                return;

            lastBalance = balance;
            OnBalanceChanged?.Invoke(balance);
        }

        /// <summary>
        /// The backing container changed outside a wallet operation - the player dragged a
        /// coin in or out of the grid. A wallet op in flight settles the event itself in
        /// <see cref="EndMutation"/>, so this only acts when none is.
        /// </summary>
        private void OnCoinsChanged(Dictionary<Vector2Int, Package> _)
        {
            if (mutating <= 0)
                RaiseBalanceIfChanged();
        }

        /// <summary>Removes every denomination's count in <paramref name="coinsToRemove"/>.</summary>
        private void RemoveCoins(Currency coinsToRemove)
        {
            foreach (var type in Currency.Denominations)
                RemoveCurrency(type, coinsToRemove.CountOf(type));
        }

        /// <summary>
        /// Removes <paramref name="amount"/> base coins of <paramref name="type"/> from the
        /// backing container, matching on <see cref="CurrencyType"/> rather than reference
        /// equality - the remove-side mirror of <see cref="CalculateCash"/>.
        /// </summary>
        private void RemoveCurrency(CurrencyType type, uint amount)
        {
            if (0u == amount)
                return;

            foreach (var position in coins.StoredPackages.Keys.ToList())
            {
                if (0u == amount)
                    break;

                if (!coins.StoredPackages.TryGetValue(position, out var stored))
                    continue;

                var view = coins.ViewOf(stored.Item);
                if (!view.IsCurrency || view.Definition.CurrencyType != type)
                    continue;

                var take = Math.Min(amount, stored.Amount);
                _ = coins.RemoveAtPosition(position, new Package(coins, stored.Item, take));
                amount -= take;
            }

            if (0u < amount)
                Debug.LogWarning($"{nameof(RemoveCurrency)}: {amount} {type} left unremoved - wallet desync?");
        }

        /// <summary>Sums the backing container's currency packages into a <see cref="Currency"/>.</summary>
        private Currency CalculateCash()
        {
            var cash = default(Currency);

            foreach (var package in coins.StoredPackages)
            {
                var view = coins.ViewOf(package.Value.Item);
                if (!view.IsCurrency)
                    continue;

                var type = view.Definition.CurrencyType;
                cash = cash.With(type, cash.CountOf(type) + package.Value.Amount);
            }

            return cash;
        }
    }
}
