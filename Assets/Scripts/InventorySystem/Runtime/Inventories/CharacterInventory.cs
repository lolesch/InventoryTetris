using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    [System.Serializable]
    public class CharacterInventory : AbstractDimensionalContainer
    {
        public CharacterInventory(Vector2Int dimensions) : base(dimensions) { }
        public override bool TryAddToContainer(ref Package package)
        {
            if (!package.IsValid)
                return false;

            /// TryStack
            _ = TryStack(ref package);

            /// TryAddToEmpty
            _ = TryAddAtEmpty(ref package);

            if (AutoConsolidate)
                Consolidate();

            InvokeRefresh();

            return 0 == package.Amount;
        }

        public override Package AddAtPosition(Vector2Int position, Package package)
        {
            if (!package.IsValid)
                return package;

            var dimensions = AbstractItem.GetDimensions(package.Item.Dimensions);

            if (IsEmptySpace(position, dimensions, out var otherItems))
                TryAddToInventory();
            else if (1 == otherItems.Count)
                if (StoredPackages.TryGetValue(otherItems[0], out var storedPackage))
                    if (!TryStack(storedPackage, otherItems[0]))
                        TrySwap(storedPackage, otherItems[0]);

            InvokeRefresh();

            return package;

            void TryAddToInventory()
            {
                var amount = Math.Min(package.Amount, (uint)package.Item.StackLimit);

                if (StoredPackages.TryAdd(position, new Package(this, package.Item, amount)))
                    _ = package.ReduceAmount(amount);
            }

            bool TryStack(Package storedPackage, Vector2Int storedPosition)
            {
                if (0 == storedPackage.SpaceLeft)
                    return false;

                if (!package.Item.Equals(storedPackage.Item))
                    return false;

                var addedAmount = storedPackage.IncreaseAmount(package.Amount);
                _ = package.ReduceAmount(addedAmount);

                StoredPackages[storedPosition] = storedPackage;

                return true;
            }

            void TrySwap(Package storedPackage, Vector2Int storedPosition)
            {
                _ = RemoveAtPosition(storedPosition, storedPackage);

                TryAddToInventory();

                // TODO: check for item loss, else revert
                package = storedPackage;
            }
        }

        public override List<Vector2Int> GetStoredItemsAt(Vector2Int position, Vector2Int dimension)
        {
            List<Vector2Int> otherPackagePositions = new();
            var requiredPositions = CalculateRequiredPositions(position, dimension);

            foreach (var package in StoredPackages)
                for (var x = package.Key.x; x < package.Key.x + AbstractItem.GetDimensions(package.Value.Item.Dimensions).x; x++)
                    for (var y = package.Key.y; y < package.Key.y + AbstractItem.GetDimensions(package.Value.Item.Dimensions).y; y++)
                        foreach (var requiredPosition in requiredPositions)
                            if (new Vector2Int(x, y) == requiredPosition)
                                otherPackagePositions.Add(package.Key);

            return otherPackagePositions.Distinct().ToList();
        }

        public bool TryPay(float buyValue)
        {
            if (!CalculateCash().TryGetPayment(new Currency(buyValue), out var toRemove, out var change))
                return false;

            RemoveCurrency(CurrencyType.Copper, toRemove.Copper);
            RemoveCurrency(CurrencyType.Iron, toRemove.Iron);
            RemoveCurrency(CurrencyType.Silver, toRemove.Silver);
            RemoveCurrency(CurrencyType.Gold, toRemove.Gold);

            if (0u < change.Total)
                AddChange(change);

            return true;
        }

        public bool CanAfford(float buyValue) => new Currency(buyValue).Total <= CalculateCash().Total;

        /// <summary>
        /// Removes <paramref name="amount"/> coins of <paramref name="type"/> from the
        /// stored currency packages. The remove-side mirror of <see cref="CalculateCash"/>;
        /// unlike RemoveFromContainer it matches on CurrencyType, not reference equality.
        /// </summary>
        private void RemoveCurrency(CurrencyType type, uint amount)
        {
            if (0u == amount)
                return;

            foreach (var position in StoredPackages.Keys.ToList())
            {
                if (0u == amount)
                    break;

                if (!StoredPackages.TryGetValue(position, out var stored))
                    continue;

                if (stored.Item is not CurrencyItem coin || coin.CurrencyType != type)
                    continue;

                var take = Math.Min(amount, stored.Amount);
                _ = RemoveAtPosition(position, new Package(this, stored.Item, take));
                amount -= take;
            }

            if (0u < amount)
                Debug.LogWarning($"{nameof(RemoveCurrency)}: {amount} {type} left unremoved - wallet desync?");
        }

        /// <summary>
        /// Returns coins to the inventory, largest denomination first. Used both for
        /// change (where each denomination is always below its stack limit) and for
        /// <see cref="Consolidate"/> (where it may not be - TryAddToContainer spills
        /// the overflow into further cells).
        /// The full-inventory edge (change dropped) is acknowledged - see
        /// dev/specs/2026-08-26-shop-currency-followups.md section 2.
        /// </summary>
        private void AddChange(Currency change)
        {
            AddCoins(CurrencyType.Gold, change.Gold);
            AddCoins(CurrencyType.Silver, change.Silver);
            AddCoins(CurrencyType.Iron, change.Iron);
            AddCoins(CurrencyType.Copper, change.Copper);

            void AddCoins(CurrencyType type, uint count)
            {
                if (0u == count)
                    return;

                var package = new Package(this, ItemProvider.Instance.GenerateCurrency(type), count);
                _ = TryAddToContainer(ref package);
            }
        }

        private Currency CalculateCash()
        {
            uint copper = 0;
            uint iron = 0;
            uint silver = 0;
            uint gold = 0;

            foreach (var package in StoredPackages)
            {
                if (package.Value.Item is not CurrencyItem currencyItem)
                    continue;

                if (currencyItem.CurrencyType == CurrencyType.Copper)
                    copper += package.Value.Amount;
                else if (currencyItem.CurrencyType == CurrencyType.Iron)
                    iron += package.Value.Amount;
                else if (currencyItem.CurrencyType == CurrencyType.Silver)
                    silver += package.Value.Amount;
                else if (currencyItem.CurrencyType == CurrencyType.Gold)
                    gold += package.Value.Amount;
            }

            return new Currency( iron, copper, silver, gold );
        }

        private bool isConsolidating;

        /// <summary>
        /// Folds every stored coin into the largest denominations that fit, leaving the
        /// remainder as loose change. Value-preserving: Total before == Total after.
        /// Re-entrancy is guarded because <see cref="AddChange"/> re-enters
        /// <see cref="TryAddToContainer"/>, which is where <see cref="AutoConsolidate"/>
        /// is honoured.
        /// </summary>
        public void Consolidate()
        {
            if (isConsolidating)
                return;

            var wallet = CalculateCash();

            if (0u == wallet.Total)
                return;

            var consolidated = new Currency(wallet.Total);

            if (consolidated.Iron == wallet.Iron
                && consolidated.Copper == wallet.Copper
                && consolidated.Silver == wallet.Silver
                && consolidated.Gold == wallet.Gold)
                return; // already canonical - don't churn the grid

            isConsolidating = true;

            try
            {
                RemoveCurrency(CurrencyType.Iron, wallet.Iron);
                RemoveCurrency(CurrencyType.Copper, wallet.Copper);
                RemoveCurrency(CurrencyType.Silver, wallet.Silver);
                RemoveCurrency(CurrencyType.Gold, wallet.Gold);

                AddChange(consolidated);
            }
            finally
            {
                isConsolidating = false;
            }

            InvokeRefresh();
        }
    }
}
