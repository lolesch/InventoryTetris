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
    public sealed class CharacterEquipment : AbstractDimensionalContainer
    {
        private readonly IStatReceiver statReceiver;

        /// <summary>Guards the force-swap against re-entering itself: a displaced item
        /// re-homing back through this container must not trigger a second swap (issue #12).</summary>
        private bool midForceSwap;

        /// <param name="statReceiver">The character worn items apply their affixes to.
        /// Null in a pure container test that only exercises placement.</param>
        public CharacterEquipment(Vector2Int dimensions, IItemCatalog catalog, IStatReceiver statReceiver = null) : base(dimensions, catalog) =>
            this.statReceiver = statReceiver;

        protected override void OnPackageRemoved(Package package)
        {
            var affixes = package.Item.Affixes;
            RunOrQueue(() => statReceiver?.RemoveItemStats(affixes));
        }

        [SerializeField] public bool autoEquip = true;

        public override bool TryAddToContainer(ref Package package) => TryAddToContainer(ref package, 0);

        /// <param name="preferredSlotIndex">Which of <see cref="GetTypeSpecificPositions"/>'s
        /// slots to target, for equipment types with more than one (rings, dual-wielded 1H
        /// weapons). 0 is the default: the first empty slot, else a force-swap. Above 0 is a
        /// right-click modifier's explicit choice of that slot - it goes there, empty or
        /// worn, and never falls back to another. It can only name a slot the item's type
        /// may enter, so a bow or shield (one slot each) lands on its own slot whatever the
        /// index. Out-of-range values clamp to the last slot.</param>
        public bool TryAddToContainer(ref Package package, int preferredSlotIndex)
        {
            if (!package.IsValid || !IsEquipment(package.Item))
                return false;

            var equipmentType = EquipmentTypeOf(package.Item);

            // AddAtPosition fills the slot if it is empty and force-swaps if it is not.
            if (SwapTarget(equipmentType, preferredSlotIndex) is { } target)
                package = AddAtPosition(target, package);
            else
                _ = TryAddAtEmpty(ref package, 0);

            InvokeRefresh();

            // A force-swap that gave up (issue #12) leaves the item unplaced; report that so
            // a re-home cascade routing through here can fail cleanly instead of losing it.
            return 0 == package.Amount;
        }

        /// <summary>
        /// The one decision of where an equip goes, shared by the equip itself and by the tooltip that
        /// previews it (<see cref="CompareTargets"/>), so the preview can never promise a swap the equip will
        /// not do. <c>null</c>: the default equip (index 0) finds a free slot and displaces nothing. Otherwise
        /// the slot it targets - an explicit choice (index above 0), or, with every slot full, the
        /// <see cref="ForceSwapPosition"/> - and what is worn there is what goes.
        /// </summary>
        private Vector2Int? SwapTarget(EquipmentType equipmentType, int preferredSlotIndex)
        {
            var positions = GetTypeSpecificPositions(equipmentType);
            var slotIndex = Math.Clamp(preferredSlotIndex, 0, positions.Length - 1);

            if (0 < preferredSlotIndex)
                return positions[slotIndex];

            var footprint = SlotFootprint(equipmentType);

            return positions.Any(position => IsEmptySpace(position, footprint, out _))
                ? null
                : ForceSwapPosition(equipmentType, positions, slotIndex);
        }

        /// <summary>
        /// Where a default equip into full slots swaps: the first slot holding a different type of gear
        /// (a bow in front of a shield is the one that goes), else the preferred one. The one rule shared
        /// by the equip itself and by the tooltip that previews it.
        /// </summary>
        private Vector2Int ForceSwapPosition(EquipmentType equipmentType, Vector2Int[] equipmentPositions, int slotIndex)
        {
            // TryGetValue, not the raw indexer (issue #12): a type-specific position is
            // not always a live key - a 2H is keyed only at the weapon slot, so reading
            // StoredPackages[(13,0)] for the off-hand threw KeyNotFoundException.
            var preferedPosition = equipmentPositions.Where(x =>
                StoredPackages.TryGetValue(x, out var stored)
                && stored.Item != null
                && EquipmentTypeOf(stored.Item) != equipmentType);

            return preferedPosition.Any() ? preferedPosition.First() : equipmentPositions[slotIndex];
        }

        protected override bool TryAddAtEmpty(ref Package package) => TryAddAtEmpty(ref package, 0);

        private bool TryAddAtEmpty(ref Package package, int preferredSlotIndex)
        {
            if (!package.IsValid || !IsEquipment(package.Item))
                return false;

            var equipmentType = EquipmentTypeOf(package.Item);
            var dimensions = SlotFootprint(equipmentType);

            var typePositions = GetTypeSpecificPositions(equipmentType);
            var slotIndex = Math.Clamp(preferredSlotIndex, 0, typePositions.Length - 1);
            // Try the preferred slot first so a modifier-driven equip onto an empty slot
            // respects the same preference a force-swap would - not just the fallback order.
            var orderedPositions = typePositions.Skip(slotIndex).Concat(typePositions.Take(slotIndex));

            foreach (var position in orderedPositions)
                if (IsEmptySpace(position, dimensions, out _))
                    package = AddAtPosition(position, package);

            if (0 < package.Amount)
                Debug.LogWarning($"{GetType().Name} is full!");

            return 0 == package.Amount;
        }

        public bool AutoEquip(ref Package package) => TryAddAtEmpty(ref package, 0);

        public override Package AddAtPosition(Vector2Int position, Package package)
        {
            if (!package.IsValid || !IsEquipment(package.Item))
                return package;

            // A two-hander dropped on the off-hand slot its 2-wide footprint fills still
            // anchors at the weapon slot (issue #42); `position` stays the cell the player
            // actually dropped on so TrySwap can still tell what is directly under the drop.
            var anchor = AnchorOf(position, package.Item);

            var dimensions = SlotFootprintOf(package.Item);

            if (IsEmptySpace(anchor, dimensions, out var otherItems))
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
                var stackLimit = ViewOf(package.Item).StackLimit;
                if (1u < stackLimit)
                    Debug.LogWarning($"EquipmentItems should not be stackable! {stackLimit}");

                var amount = Math.Min(package.Amount, stackLimit);

                if (StoredPackages.TryAdd(anchor, new Package(this, package.Item, amount)))
                {
                    var affixes = package.Item.Affixes;
                    RunOrQueue(() => statReceiver?.AddItemStats(affixes));

                    _ = package.ReduceAmount(amount);
                }
            }

            void TrySwap(List<Vector2Int> positions)
            {
                var dropPosition = position;
                var collateral = new List<(Vector2Int Position, Package Package)>();
                var underDrop = default(Package);
                var underDropPosition = default(Vector2Int);

                foreach (var occupied in positions)
                    if (StoredPackages.TryGetValue(occupied, out var storedPackage))
                        if (storedPackage.Item != null && 0 < storedPackage.Amount)
                        {
                            // The swap partner is whatever the drop cell lands *on* - so a
                            // two-hander keyed at (12,0) is the partner for a drop on (13,0)
                            // its footprint spans, not miscounted as collateral (issue #42).
                            // Each displaced item's own slot is kept alongside it - a cancel
                            // that lands on the cursor mid-drag must return it there, not to
                            // wherever the drag itself started (issue #29 follow-up).
                            if (FootprintContains(occupied, SlotFootprintOf(storedPackage.Item), dropPosition))
                            {
                                underDrop = storedPackage;
                                underDropPosition = occupied;
                            }
                            else
                                collateral.Add((occupied, storedPackage));
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
                        foreach (var (occupied, displaced) in collateral)
                        {
                            var reHomed = displaced;
                            if (!ActiveTransaction.TryReHomeToContainerOrHand(ref reHomed, new PackageOrigin(this, occupied)))
                                break;
                        }

                        if (underDrop.IsValid && !ActiveTransaction.Aborted)
                        {
                            var reHomed = underDrop;
                            _ = ActiveTransaction.TryReHomeToContainerOrHand(ref reHomed, new PackageOrigin(this, underDropPosition));
                        }
                    }
                    else
                    {
                        // Drag: the collateral off-hand must swap into the origin or the
                        // whole move rolls back; the swap partner goes to the hand, exactly
                        // as a plain one-item swap does.
                        foreach (var (_, displaced) in collateral)
                        {
                            var reHomed = displaced;
                            if (!ActiveTransaction.TryReHomeToContainer(ref reHomed))
                                break;
                        }

                        if (underDrop.IsValid && !ActiveTransaction.Aborted)
                        {
                            var reHomed = underDrop;
                            _ = ActiveTransaction.TryReHomeToHandOrContainer(ref reHomed, new PackageOrigin(this, underDropPosition));
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

                    package = underDrop.IsValid ? underDrop : collateral.Select(c => c.Package).FirstOrDefault();
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

        /// <summary>
        /// Return-to-origin (issue #29) against the paper-doll layout: <paramref name="position"/>
        /// must be a slot <paramref name="item"/>'s type is allowed in at all, and that slot
        /// must be genuinely empty - measured with the equipment footprint rule
        /// (<see cref="SlotFootprintOf"/>), never the item's inventory-bag
        /// <see cref="ItemView.Dimensions"/>. Unlike <see cref="CanPlaceAt"/> this never
        /// allows the swap; a cancelled drag that cannot re-key here falls through to the
        /// backpack instead of displacing whatever is now worn.
        /// </summary>
        public override bool CanReturnTo(Vector2Int position, ItemInstance item) =>
            IsEquipment(item)
            && GetTypeSpecificPositions(EquipmentTypeOf(item)).Contains(position)
            && IsEmptySpace(position, SlotFootprintOf(item), out _);

        /// <summary>
        /// Whether <see cref="AddAtPosition"/> would place <paramref name="item"/> at
        /// <paramref name="position"/> right now: it is equipment, <paramref name="position"/>
        /// is a slot this equipment type is allowed in, and the slot - with the swap it may
        /// trigger - can take it. The exact predicate <c>EquipmentSlotDisplay.DropItem</c>
        /// gates on, so the red "can't drop" tint and the drop can never disagree (issue #12).
        /// The footprint is <see cref="SlotFootprintOf"/> - the paper-doll layout's own rule -
        /// never the item's inventory-bag <see cref="ItemView.Dimensions"/>, which runs off
        /// the 1-tall equipment row and reddened every hover of a helm or a sword over its
        /// own empty slot.
        /// </summary>
        public bool CanEquipAt(Vector2Int position, ItemInstance item)
        {
            if (!IsEquipment(item))
                return false;

            // A two-hander hovered over the off-hand slot it visually fills resolves to its
            // weapon-slot anchor - so the tint accepts the drop the same place AddAtPosition
            // will land it (issue #42).
            var anchor = AnchorOf(position, item);

            return GetTypeSpecificPositions(EquipmentTypeOf(item)).Contains(anchor)
                && CanPlaceAt(anchor, SlotFootprintOf(item));
        }

        /// <summary>
        /// What a hover tooltip compares <paramref name="item"/> against - the worn gear an equip
        /// would displace. Looked up by the slot cells the item would fill, so a worn two-hander
        /// (keyed only at the weapon slot) answers for the off-hand cell it covers too, and a
        /// hovered two-hander meets both the weapon and the off-hand it would push out.
        /// </summary>
        /// <param name="secondSlot">Shift: the equip would target the second slot, so its
        /// occupant leads and the display order flips wherever two slots are in play.</param>
        /// <returns><c>Shown</c>: each distinct worn item once, in display order (at most two).
        /// <c>Against</c>: the items an equip would actually displace and the hovered one's stat rows are
        /// measured against - the targeted slot's occupant, for a two-hander everything it pushes out,
        /// and nothing when the equip lands in a free slot.</returns>
        public (IReadOnlyList<Package> Shown, IReadOnlyList<Package> Against) CompareTargets(ItemInstance item, bool secondSlot)
        {
            var shown = new List<Package>(2);
            var against = new List<Package>(2);

            if (!IsEquipment(item))
                return (shown, against);

            var equipmentType = EquipmentTypeOf(item);
            var footprint = SlotFootprint(equipmentType);

            // One entry per cell the item can land on, in slot order: sword 12 then 13, ring 10
            // then 11, a two-hander 12 then the 13 it spills over, everything else a single cell.
            var occupants = new List<Package>(2);
            foreach (var anchor in GetTypeSpecificPositions(equipmentType))
                for (var x = 0; x < footprint.x; x++)
                    occupants.Add(OccupantOf(new Vector2Int(anchor.x + x, anchor.y)));

            if (secondSlot)
                occupants.Reverse();

            foreach (var occupant in occupants)
                if (occupant.IsValid && !shown.Exists(s => s.Item == occupant.Item))
                    shown.Add(occupant);

            // What an equip really displaces: whatever is worn where SwapTarget - the same decision the equip
            // makes - sends it. A free slot displaces nothing at all. A two-hander pushes out everything its
            // footprint covers, which is all of `shown`.
            if (IsTwoHandedWeapon(equipmentType))
                against.AddRange(shown);
            else if (SwapTarget(equipmentType, secondSlot ? 1 : 0) is { } target && OccupantOf(target) is { IsValid: true } displaced)
                against.Add(displaced);

            return (shown, against);

            Package OccupantOf(Vector2Int cell)
            {
                foreach (var stored in StoredPackages)
                    if (stored.Value.Item != null && FootprintContains(stored.Key, SlotFootprintOf(stored.Value.Item), cell))
                        return stored.Value;

                return default;
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
                var dimensions = SlotFootprint(EquipmentTypeOf(package.Value.Item));

                for (var x = package.Key.x; x < package.Key.x + dimensions.x; x++)
                    for (var y = package.Key.y; y < package.Key.y + dimensions.y; y++)
                        foreach (var requiredPosition in requiredPositions)
                            if (new Vector2Int(x, y) == requiredPosition)
                                otherPackagePositions.Add(package.Key);
            }

            return otherPackagePositions.Distinct().ToList();
        }

        /// <summary>Whether a stored instance is equipment at all - the check that was <c>is EquipmentItem</c>.</summary>
        private bool IsEquipment(ItemInstance item) =>
            item != null && ViewOf(item).Definition.Category == ItemCategory.Equipment;

        /// <summary>The slot type a stored instance fills - was <c>(item as EquipmentItem).EquipmentType</c>.</summary>
        private EquipmentType EquipmentTypeOf(ItemInstance item) =>
            ViewOf(item).Definition.EquipmentType;

        public static bool IsTwoHandedWeapon(EquipmentType equipmentType) => equipmentType is > EquipmentType.TWOHANDEDWEAPONS and < EquipmentType.OFFHANDS;

        /// <summary>
        /// The slot <see cref="AddAtPosition"/> keys <paramref name="item"/> under for a drop
        /// on <paramref name="position"/>. A two-hander dropped anywhere under its own 2-wide
        /// footprint - including the off-hand slot it visually fills - anchors at the weapon
        /// slot (issue #42); every other item is keyed where it was dropped.
        /// </summary>
        private Vector2Int AnchorOf(Vector2Int position, ItemInstance item)
        {
            var equipmentType = EquipmentTypeOf(item);

            if (!IsTwoHandedWeapon(equipmentType))
                return position;

            var anchor = GetTypeSpecificPositions(equipmentType)[0];
            return FootprintContains(anchor, SlotFootprint(equipmentType), position) ? anchor : position;
        }

        /// <summary>Whether the <paramref name="footprint"/>-sized rect keyed at <paramref name="key"/> covers <paramref name="cell"/>.</summary>
        private static bool FootprintContains(Vector2Int key, Vector2Int footprint, Vector2Int cell) =>
            key.x <= cell.x && cell.x < key.x + footprint.x &&
            key.y <= cell.y && cell.y < key.y + footprint.y;

        /// <summary>
        /// The slots a worn item spans in the 14x1 equipment strip: two for a two-hander,
        /// one for everything else. This is the paper-doll layout's own footprint rule; an
        /// item's <see cref="ItemView.Dimensions"/> is its inventory-bag shape and is
        /// meaningless here (issue #12).
        /// </summary>
        public Vector2Int SlotFootprintOf(ItemInstance item) => SlotFootprint(EquipmentTypeOf(item));

        private static Vector2Int SlotFootprint(EquipmentType equipmentType) =>
            IsTwoHandedWeapon(equipmentType) ? new Vector2Int(2, 1) : new Vector2Int(1, 1);

        /// <summary>Slot index of the main hand; a two-handed weapon also claims <see cref="OffHandSlot"/>.</summary>
        public const int MainHandSlot = 12;
        public const int OffHandSlot = 13;

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

            EquipmentType.Bow => new Vector2Int[1] { new(MainHandSlot, 0) },
            // dualWield
            > EquipmentType.ONEHANDEDWEAPONS and < EquipmentType.TWOHANDEDWEAPONS => new Vector2Int[2] { new(MainHandSlot, 0), new(OffHandSlot, 0) },

            > EquipmentType.TWOHANDEDWEAPONS and < EquipmentType.OFFHANDS => new Vector2Int[1] { new(MainHandSlot, 0) },

            > EquipmentType.OFFHANDS and < EquipmentType.JEWELRY => new Vector2Int[1] { new(OffHandSlot, 0) },

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
