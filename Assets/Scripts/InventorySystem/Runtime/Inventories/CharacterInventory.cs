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

                if ( storedPackage.Item is CurrencyItem currencyItem )
                    if ( storedPackage.Amount == (uint)storedPackage.Item.StackLimit ) // full stack
                        if ( CheckForCurrencyUpgrade() )
                            return true;

                StoredPackages[storedPosition] = storedPackage;

                return true;

                bool CheckForCurrencyUpgrade()
                {
                    var higherCurrency = UpgradeCurrency( currencyItem );

                    if (higherCurrency != storedPackage.Item)
                    {
                        RemoveAtPosition(storedPosition, storedPackage);

                        storedPackage = new Package(storedPackage.Sender, higherCurrency, 1u);

                        if (TryAddToContainer(ref storedPackage))
                            return true;
                    }

                    return false;

                    AbstractItem UpgradeCurrency(CurrencyItem currencyItem) => currencyItem.CurrencyType switch
                    {
                        Data.Enums.CurrencyType.Copper => new CurrencyItem(Data.Enums.CurrencyType.Iron),
                        Data.Enums.CurrencyType.Iron => new CurrencyItem(Data.Enums.CurrencyType.Silver),
                        Data.Enums.CurrencyType.Silver => new CurrencyItem(Data.Enums.CurrencyType.Gold),

                        // no upgrade
                        Data.Enums.CurrencyType.Gold => currencyItem,
                        Data.Enums.CurrencyType.NONE => currencyItem,
                        _ => currencyItem,
                    };
                }
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
        /// Returns change coins to the inventory, largest denomination first. Each
        /// denomination of a valid change amount is always below its stack limit
        /// (the overshoot at any denomination is less than that denomination's value).
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
    }
}
