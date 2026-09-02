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
        private readonly ICursorSink cursorSink;

        /// <param name="statReceiver">The character worn items apply their affixes to.
        /// Null in a pure container test that only exercises placement.</param>
        /// <param name="cursorSink">Where a displaced item is handed when no container
        /// will re-home it. Null in a test.</param>
        public CharacterEquipment(Vector2Int dimensions, IStatReceiver statReceiver = null, ICursorSink cursorSink = null) : base(dimensions)
        {
            this.statReceiver = statReceiver;
            this.cursorSink = cursorSink;
        }

        protected override void OnPackageRemoved(Package package) =>
            statReceiver?.RemoveItemStats(package.Item.Affixes);

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
                var preferedPosition = equipmentPositions.Where(x => StoredPackages[x].Item != null
                && EquipmentTypeOf(StoredPackages[x].Item) != equipmentType);
                var position = preferedPosition.Any() ? preferedPosition.First() : equipmentPositions[0];

                package = AddAtPosition(position, package);
            }

            InvokeRefresh();

            return true;
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
            /// equipping a 2H might return two 1H
            else if (otherItems.Count <= 2)
                TrySwap(otherItems);

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
                    statReceiver?.AddItemStats(package.Item.Affixes);

                    _ = package.ReduceAmount(amount);
                }
            }

            void TrySwap(List<Vector2Int> positions)
            {
                var previouslyEquipped = new List<Package>();

                foreach (var position in positions)
                    if (StoredPackages.TryGetValue(position, out var storedPackage))
                        if (storedPackage.Item != null && 0 < storedPackage.Amount)
                        {
                            previouslyEquipped.Add(storedPackage);
                            _ = RemoveAtPosition(position, storedPackage);
                        }

                TryAddToInventory();

                if (0 < package.Amount)
                    Debug.LogWarning($"Something went wrong! remaining package will be overwritten: {package}");

                for (var i = previouslyEquipped.Count; i-- > 0;)
                {
                    var current = previouslyEquipped[i];
                    if (!package.Sender.TryAddToContainer(ref current))
                        cursorSink?.ReplacePackage(previouslyEquipped[i]);
                    previouslyEquipped[i] = current;
                }

                //    if (0 < returningToSender.Amount)
                //        DragProvider.Instance.SetPackage(DragProvider.Instance.Hovered, returningToSender, Vector2Int.zero);
                //}

                package = previouslyEquipped.Where(x => x.Item != null && 0 < x.Amount).FirstOrDefault();

                // TODO: check for item loss, else revert
            }
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
