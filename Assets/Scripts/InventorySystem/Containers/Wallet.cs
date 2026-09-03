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
                RemoveCurrency(CurrencyType.Iron, toRemove.Iron);
                RemoveCurrency(CurrencyType.Copper, toRemove.Copper);
                RemoveCurrency(CurrencyType.Silver, toRemove.Silver);
                RemoveCurrency(CurrencyType.Gold, toRemove.Gold);

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
        /// Banks <paramref name="amount"/> as minted coins, largest denomination first. A
        /// denomination over its stack limit spills into further cells; a full container
        /// drops the remainder (acknowledged - dev/specs/2026-08-26-shop-currency-followups.md
        /// section 2). Re-consolidates afterwards when the backing container asks for it
        /// (<see cref="AbstractDimensionalContainer.AutoConsolidate"/>).
        /// </summary>
        public void Deposit(Currency amount)
        {
            BeginMutation();
            try
            {
                AddCoins(CurrencyType.Gold, amount.Gold);
                AddCoins(CurrencyType.Silver, amount.Silver);
                AddCoins(CurrencyType.Iron, amount.Iron);
                AddCoins(CurrencyType.Copper, amount.Copper);

                if (coins.AutoConsolidate)
                    Consolidate();
            }
            finally
            {
                EndMutation();
            }

            void AddCoins(CurrencyType type, uint count)
            {
                if (0u == count)
                    return;

                var coin = minter?.MintCurrency(type);
                if (coin == null)
                    return;

                var package = new Package(coins, coin, count);
                _ = coins.TryAddToContainer(ref package);
            }
        }

        /// <summary>
        /// Folds every coin into the largest denominations that fit, leaving the remainder
        /// loose. Value-preserving: <see cref="Balance"/> is the same before and after, so no
        /// <see cref="OnBalanceChanged"/> is raised - only the grid repaints. The re-entrancy
        /// guard is because an <see cref="AbstractDimensionalContainer.AutoConsolidate"/>
        /// container re-enters this through <see cref="Deposit"/>.
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
                && consolidated.Gold == current.Gold)
                return; // already canonical - don't churn the grid

            isConsolidating = true;
            BeginMutation();

            try
            {
                RemoveCurrency(CurrencyType.Iron, current.Iron);
                RemoveCurrency(CurrencyType.Copper, current.Copper);
                RemoveCurrency(CurrencyType.Silver, current.Silver);
                RemoveCurrency(CurrencyType.Gold, current.Gold);

                Deposit(consolidated);
            }
            finally
            {
                isConsolidating = false;
                EndMutation();
            }

            coins.InvokeRefresh();
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

                var definition = ItemView.Of(stored.Item).Definition;
                if (definition.Category != ItemCategory.Currency || definition.CurrencyType != type)
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
            uint iron = 0;
            uint copper = 0;
            uint silver = 0;
            uint gold = 0;

            foreach (var package in coins.StoredPackages)
            {
                var definition = ItemView.Of(package.Value.Item).Definition;
                if (definition.Category != ItemCategory.Currency)
                    continue;

                switch (definition.CurrencyType)
                {
                    case CurrencyType.Iron: iron += package.Value.Amount; break;
                    case CurrencyType.Copper: copper += package.Value.Amount; break;
                    case CurrencyType.Silver: silver += package.Value.Amount; break;
                    case CurrencyType.Gold: gold += package.Value.Amount; break;
                }
            }

            return new Currency(iron, copper, silver, gold);
        }
    }
}
