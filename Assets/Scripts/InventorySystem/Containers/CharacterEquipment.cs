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
    public class CharacterEquipment : AbstractDimensionalContainer
    {
        private readonly IStatReceiver statReceiver;

        /// <summary>Guards the force-swap against re-entering itself: a displaced item
        /// re-homing back through this container must not trigger a second swap (issue #12).</summary>
        private bool midForceSwap;

        /// <param name="statReceiver">The character worn items apply their affixes to.
        /// Null in a pure container test that only exercises placement.</param>
        public CharacterEquipment(Vector2Int dimensions, IStatReceiver statReceiver = null) : base(dimensions) =>
            this.statReceiver = statReceiver;

        protected override void OnPackageRemoved(Package package)
        {
            var affixes = package.Item.Affixes;
            RunOrQueue(() => statReceiver?.RemoveItemStats(affixes));
        }

        [SerializeField] public bool autoEquip = true;

        public override bool TryAddToContainer(ref Package package)
        {
            if (!package.IsValid || !IsEquipment(package.Item))
                return false;

            _ = TryAddAtEmpty(ref package);

            /// Force swap with current equipment
            if (0 < package.Amount)
            {
                var equipmentType = EquipmentTypeOf(package.Item);

                var equipmentPositions = GetTypeSpecificPositions(equipmentType);
                // TryGetValue, not the raw indexer (issue #12): a type-specific position is
                // not always a live key - a 2H is keyed only at the weapon slot, so reading
                // StoredPackages[(13,0)] for the off-hand threw KeyNotFoundException.
                var preferedPosition = equipmentPositions.Where(x =>
                    StoredPackages.TryGetValue(x, out var stored)
                    && stored.Item != null
                    && EquipmentTypeOf(stored.Item) != equipmentType);
                var position = preferedPosition.Any() ? preferedPosition.First() : equipmentPositions[0];

                package = AddAtPosition(position, package);
            }

            InvokeRefresh();

            // A force-swap that gave up (issue #12) leaves the item unplaced; report that so
            // a re-home cascade routing through here can fail cleanly instead of losing it.
            return 0 == package.Amount;
        }

        protected override bool TryAddAtEmpty(ref Package package)
        {
            if (!package.IsValid || !IsEquipment(package.Item))
                return false;

            var equipmentType = EquipmentTypeOf(package.Item);
            var dimensions = IsTwoHandedWeapon(equipmentType) ? new Vector2Int(2, 1) : new Vector2Int(1, 1);

            var typePositions = GetTypeSpecificPositions(equipmentType);

            foreach (var position in typePositions)
                if (IsEmptySpace(position, dimensions, out _))
                    package = AddAtPosition(position, package);

            if (0 < package.Amount)
                Debug.LogWarning($"{GetType().Name} is full!");

            return 0 == package.Amount;
        }

        public bool AutoEquip(ref Package package) => TryAddAtEmpty(ref package);

        public override Package AddAtPosition(Vector2Int position, Package package)
        {
            if (!package.IsValid || !IsEquipment(package.Item))
                return package;

            var dimensions = IsTwoHandedWeapon(EquipmentTypeOf(package.Item)) ? new Vector2Int(2, 1) : new Vector2Int(1, 1);

            if (IsEmptySpace(position, dimensions, out var otherItems))
                TryAddToInventory();
            /// equipping a 2H might displace a weapon *and* an off-hand
            else if (otherItems.Count is > 0 and <= 2 && !midForceSwap)
            {
                // Explicit give-up (issue #12): the swap re-homes the gear it displaces, and
                // if a re-home routes an item back through this container it must not start a
                // second swap - that unbounded recursion was QA-4's StackOverflowException.
                // The re-home fails instead, and the transaction rolls the whole move back.
                midForceSwap = true;
                try { TrySwap(otherItems); }
                finally { midForceSwap = false; }
            }

            InvokeRefresh();

            return package;

            void TryAddToInventory()
            {
                var stackLimit = ItemView.Of(package.Item).StackLimit;
                if (1u < stackLimit)
                    Debug.LogWarning($"EquipmentItems should not be stackable! {stackLimit}");

                var amount = Math.Min(package.Amount, stackLimit);

                if (StoredPackages.TryAdd(position, new Package(this, package.Item, amount)))
                {
                    var affixes = package.Item.Affixes;
                    RunOrQueue(() => statReceiver?.AddItemStats(affixes));

                    _ = package.ReduceAmount(amount);
                }
            }

            void TrySwap(List<Vector2Int> positions)
            {
                var dropPosition = position;
                var collateral = new List<Package>();
                var underDrop = default(Package);

                foreach (var occupied in positions)
                    if (StoredPackages.TryGetValue(occupied, out var storedPackage))
                        if (storedPackage.Item != null && 0 < storedPackage.Amount)
                        {
                            if (occupied == dropPosition)
                                underDrop = storedPackage;
                            else
                                collateral.Add(storedPackage);
                            _ = RemoveAtPosition(occupied, storedPackage);
                        }

                TryAddToInventory();

                if (0 < package.Amount)
                    Debug.LogWarning($"Something went wrong! remaining package will be overwritten: {package}");

                if (ActiveTransaction != null)
                {
                    // Routed move (issue #10). The item directly under the drop is the swap
                    // partner; a 2H over a weapon and off-hand also sheds one collateral item.
                    if (ActiveTransaction.SwapsInPlace)
                    {
                        // Right-click: every displaced item swaps back into the origin, and
                        // at most one that will not re-fit overflows to the hand. A second
                        // homeless item aborts and the whole equip rolls back.
                        foreach (var displaced in collateral)
                        {
                            var reHomed = displaced;
                            if (!ActiveTransaction.TryReHomeToContainerOrHand(ref reHomed))
                                break;
                        }

                        if (underDrop.IsValid && !ActiveTransaction.Aborted)
                        {
                            var reHomed = underDrop;
                            _ = ActiveTransaction.TryReHomeToContainerOrHand(ref reHomed);
                        }
                    }
                    else
                    {
                        // Drag: the collateral off-hand must swap into the origin or the
                        // whole move rolls back; the swap partner goes to the hand, exactly
                        // as a plain one-item swap does.
                        foreach (var displaced in collateral)
                        {
                            var reHomed = displaced;
                            if (!ActiveTransaction.TryReHomeToContainer(ref reHomed))
                                break;
                        }

                        if (underDrop.IsValid && !ActiveTransaction.Aborted)
                        {
                            var reHomed = underDrop;
                            _ = ActiveTransaction.TryReHomeToHandOrContainer(ref reHomed);
                        }
                    }

                    package = default;
                }
                else
                {
                    // No transaction (a bare container test): hand the displaced gear back
                    // through `package`, exactly as CharacterInventory.AddAtPosition does.
                    // Every player-driven swap runs inside an ItemTransaction, which owns the
                    // re-home cascade and the commit / rollback; the pre-#10 path that
                    // re-homed through package.Sender - the source of QA-4's recursion - is
                    // gone (issue #12).
                    if (0 < collateral.Count)
                        Debug.LogWarning($"{GetType().Name}: a 2H double-swap needs an ItemTransaction to re-home both displaced items; {collateral.Count} would be dropped.");

                    package = underDrop.IsValid ? underDrop : collateral.FirstOrDefault();
                }
            }
        }

        /// <summary>
        /// The same verdict <see cref="AddAtPosition"/> places by (issue #12), so the red
        /// "can't drop" tint agrees with the drop: in bounds, and a two-hander landing over
        /// a weapon <em>and</em> an off-hand legally displaces both - up to two overlaps
        /// still place, where a plain container accepts only one.
        /// </summary>
        public override bool CanPlaceAt(Vector2Int position, Vector2Int dimension)
        {
            if (IsEmptySpace(position, dimension, out var otherItems))
                return true;

            // IsEmptySpace returns false with an empty list when the footprint runs off the
            // grid; a populated list means a real overlap the swap can take.
            return otherItems.Count is > 0 and <= 2;
        }

        public override List<Vector2Int> GetStoredItemsAt(Vector2Int position, Vector2Int dimension)
        {
            List<Vector2Int> otherPackagePositions = new();

            // move dimensionCalculation up here? 
            //                var dimensions = IsTwoHandedWeapon(equipmentType)
            var requiredPositions = CalculateRequiredPositions(position, dimension);

            foreach (var package in StoredPackages)
            {
                var equipmentType = EquipmentTypeOf(package.Value.Item);
                var dimensions = IsTwoHandedWeapon(equipmentType)
                    ? new Vector2Int(2, 1)
                    : new Vector2Int(1, 1);

                for (var x = package.Key.x; x < package.Key.x + dimensions.x; x++)
                    for (var y = package.Key.y; y < package.Key.y + dimensions.y; y++)
                        foreach (var requiredPosition in requiredPositions)
                            if (new Vector2Int(x, y) == requiredPosition)
                                otherPackagePositions.Add(package.Key);
            }

            return otherPackagePositions.Distinct().ToList();
        }

        /// <summary>Whether a stored instance is equipment at all - the check that was <c>is EquipmentItem</c>.</summary>
        private static bool IsEquipment(ItemInstance item) =>
            item != null && ItemView.Of(item).Definition.Category == ItemCategory.Equipment;

        /// <summary>The slot type a stored instance fills - was <c>(item as EquipmentItem).EquipmentType</c>.</summary>
        private static EquipmentType EquipmentTypeOf(ItemInstance item) =>
            ItemView.Of(item).Definition.EquipmentType;

        public static bool IsTwoHandedWeapon(EquipmentType equipmentType) => equipmentType is > EquipmentType.TWOHANDEDWEAPONS and < EquipmentType.OFFHANDS;

        public static Vector2Int[] GetTypeSpecificPositions(EquipmentType equipment) => equipment switch
        {
            EquipmentType.Amulet => new Vector2Int[1] { new(0, 0) },
            EquipmentType.Belt => new Vector2Int[1] { new(1, 0) },
            EquipmentType.Boots => new Vector2Int[1] { new(2, 0) },
            EquipmentType.Bracers => new Vector2Int[1] { new(3, 0) },
            EquipmentType.Chest => new Vector2Int[1] { new(4, 0) },
            EquipmentType.Cloak => new Vector2Int[1] { new(5, 0) },
            EquipmentType.Gloves => new Vector2Int[1] { new(6, 0) },
            EquipmentType.Helm => new Vector2Int[1] { new(7, 0) },
            EquipmentType.Pants => new Vector2Int[1] { new(8, 0) },
            EquipmentType.Shoulders => new Vector2Int[1] { new(9, 0) },

            EquipmentType.Ring => new Vector2Int[2] { new(10, 0), new(11, 0) },

            EquipmentType.Bow => new Vector2Int[1] { new(12, 0) },
            // dualWield
            > EquipmentType.ONEHANDEDWEAPONS and < EquipmentType.TWOHANDEDWEAPONS => new Vector2Int[2] { new(12, 0), new(13, 0) },

            > EquipmentType.TWOHANDEDWEAPONS and < EquipmentType.OFFHANDS => new Vector2Int[1] { new(12, 0) },

            > EquipmentType.OFFHANDS and < EquipmentType.JEWELRY => new Vector2Int[1] { new(13, 0) },

            #region INVALID REQUESTS
            EquipmentType.NONE => new Vector2Int[1] { new(-1, -1) },
            EquipmentType.ARMAMENTS => new Vector2Int[1] { new(-1, -1) },
            EquipmentType.ONEHANDEDWEAPONS => new Vector2Int[1] { new(-1, -1) },
            EquipmentType.TWOHANDEDWEAPONS => new Vector2Int[1] { new(-1, -1) },
            EquipmentType.OFFHANDS => new Vector2Int[1] { new(-1, -1) },
            EquipmentType.JEWELRY => new Vector2Int[1] { new(-1, -1) },
            _ => new Vector2Int[1] { new(-1, -1) },
            #endregion INVALID REQUESTS
        };
    }
}
