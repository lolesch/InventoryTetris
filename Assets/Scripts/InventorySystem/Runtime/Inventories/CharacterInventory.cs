using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
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
            if (package.Item == null || package.Amount <= 0)
                return package;

            var dimensions = AbstractItem.GetDimensions(package.Item.Dimensions);

            if (IsEmptySpace(position, dimensions, out var otherItems))
                TryAddToInventory();
            else if (1 == otherItems.Count)
                if (StoredPackages.TryGetValue(otherItems[0], out var storedPackage))
                    if (!TryStack(storedPackage))
                        TrySwap(storedPackage);

            InvokeRefresh();

            return package;

            void TryAddToInventory()
            {
                var amount = Math.Min(package.Amount, (uint)package.Item.StackLimit);

                if (StoredPackages.TryAdd(position, new Package(this, package.Item, amount)))
                    _ = package.ReduceAmount(amount);
            }

            bool TryStack(Package storedPackage)
            {
                if (0 < storedPackage.SpaceLeft)
                    if (package.Item.Equals(storedPackage.Item))
                    {
                        var addedAmount = storedPackage.IncreaseAmount(package.Amount);
                        _ = package.ReduceAmount(addedAmount);


                        if (storedPackage.Item is CurrencyItem)
                            if (storedPackage.Amount == (uint)storedPackage.Item.StackLimit) // full stack
                                if (CheckForCurrencyUpgrade())
                                    return true;

                        StoredPackages[position] = storedPackage;

                        return true;

                        bool CheckForCurrencyUpgrade()
                        {
                            var higherCurrency = UpgradeCurrency(storedPackage.Item as CurrencyItem);

                            if (higherCurrency != storedPackage.Item)
                            {
                                RemoveAtPosition(position, storedPackage);

                                storedPackage = new Package(storedPackage.Sender, higherCurrency);

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

                return false;
            }

            void TrySwap(Package storedPackage)
            {
                _ = RemoveAtPosition(position, storedPackage);

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
    }

    // CONTINUE HERE ...
    public class VendorSupply : AbstractDimensionalContainer
    {
        public VendorSupply(Vector2Int dimensions) : base(dimensions) { }

        public override Package AddAtPosition(Vector2Int position, Package package)
            => throw new NotImplementedException();
        public override List<Vector2Int> GetStoredItemsAt(Vector2Int position, Vector2Int dimension) => throw new NotImplementedException();

        public void Restock() { }
    }
}
