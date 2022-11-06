using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests
{
    public class ContainerTest
    {
        protected static void Release(Object obj) => Object.DestroyImmediate(obj);

        private static void SetupOtherItem(out Package other, ItemStackType stackLimit = ItemStackType.Single, uint amount = 1u)
        {
            other = new Package(ScriptableObject.CreateInstance<Item>(), amount);
            other.Item.SetStackLimit(stackLimit);
        }

        private static void SetupContainer(out PlayerInventory container, out Package package, ItemStackType stackLimit = ItemStackType.Single, uint amount = 1u, int dimensionX = 5, int dimensionY = 3)
        {
            container = new PlayerInventory(new(dimensionX, dimensionY));
            package = new Package(ScriptableObject.CreateInstance<Item>(), 0);
            package.Item.SetStackLimit(stackLimit);
            package.IncreaseAmount(amount);
        }

        //assemble, act, assert, release

        [Test]
        public void CanAddToContainer()
        {
            SetupContainer(out var container, out var package);

            var remaining = container.AddToContainer(package).Amount;

            Assert.IsTrue(container.storedPackages.ContainsValue(package));
            Assert.IsTrue(remaining == 0);

            Release(package.Item);
        }

        [Test]
        public void CanAddToStacksBeforeEmpltyPositions()
        {
            SetupContainer(out var container, out var stackable, ItemStackType.StackOfTen, 9);

            container.AddAtPosition(new(0, 1), stackable);
            container.AddToContainer(stackable);

            Assert.AreEqual(1, container.storedPackages[new(0, 0)].Amount);
        }

        [Test]
        public void CanAddToOpenedStacks()
        {
            SetupContainer(out var container, out var stackable, ItemStackType.StackOfTen);

            container.AddToContainer(stackable);
            container.AddToContainer(stackable);

            Assert.AreEqual(1, container.storedPackages.Count);

            Release(stackable.Item);
        }

        [Test]
        public void CanAddAtPosition()
        {
            SetupContainer(out var container, out var package);

            container.AddAtPosition(Vector2Int.zero, package);

            Assert.AreEqual(package, container.storedPackages[Vector2Int.zero]);
            Release(package.Item);
        }

        [Test]
        public void CanAddToMultipleOpenedStacks()
        {
            SetupContainer(out var container, out var stackable, ItemStackType.StackOfTen, 9);

            for (var x = 0; x < container.Dimensions.x; x++)
                for (var y = 0; y < container.Dimensions.y; y++)
                    container.AddAtPosition(new(x, y), stackable);

            container.AddToContainer(stackable);

            uint amount = 0;
            foreach (var package in container.storedPackages.Values)
                amount += package.Amount;

            Assert.AreEqual(container.Capacity * (uint)ItemStackType.StackOfTen, amount);
            Release(stackable.Item);
        }

        [Test]
        public void CanAddMultipleItems()
        {
            SetupContainer(out var container, out var package);

            package = new Package(package.Item, 15);
            container.AddToContainer(package);

            Assert.AreEqual(15, container.storedPackages.Count);
            Release(package.Item);
        }

        [Test]
        public void HasEmptyDimensions()
        {
            SetupContainer(out var container, out var package);

            container.AddToContainer(package);

            Vector2Int position = new(-1, -1);

            for (var x = 0; x < container.Dimensions.x && 0 < package.Amount; x++)
                for (var y = 0; y < container.Dimensions.y && 0 < package.Amount; y++)
                    if (container.IsValidPosition(new(x, y), package.Item.Dimensions))
                        if (container.IsEmptyPosition(new(x, y), package.Item.Dimensions))
                            position = new(x, y);

            Assert.IsTrue(container.IsWithinDimensions(position));
            Release(package.Item);
        }

        [Test]
        public void IsFull()
        {
            SetupContainer(out var container, out var package);

            for (var i = 0; i < container.Capacity; i++)
                container.AddToContainer(package);

            var isFull = true;

            for (var x = 0; x < container.Dimensions.x && 0 < package.Amount; x++)
                for (var y = 0; y < container.Dimensions.y && 0 < package.Amount; y++)
                    if (container.IsValidPosition(new(x, y), package.Item.Dimensions))
                        if (container.IsEmptyPosition(new(x, y), package.Item.Dimensions))
                            isFull = false;

            Assert.IsTrue(isFull);
            Release(package.Item);
        }

        [Test]
        public void IsOccupiedAtPosition()
        {
            SetupContainer(out var container, out var package);

            for (var i = 0; i < container.Capacity; i++)
                container.AddToContainer(package);

            var others = container.GetStoredPackagePositionsAt(Vector2Int.zero, new(2, 2)).Count;

            Assert.IsTrue(0 < others);
            Release(package.Item);
        }

        [Test]
        public void IsWithinDimensions()
        {
            SetupContainer(out var container, out var package);

            var isTooSmall = container.IsWithinDimensions(new(-1, -1));
            var isTooBigX = container.IsWithinDimensions(new(container.Dimensions.x, 0));
            var isTooBigY = container.IsWithinDimensions(new(0, container.Dimensions.y));

            Assert.IsTrue(!isTooSmall && !isTooBigX && !isTooBigY);
            Release(package.Item);
        }

        [Test]
        public void CanStackItems([Values(ItemStackType.NONE, ItemStackType.Single, ItemStackType.StackOfTen, ItemStackType.StackOfHundred)] ItemStackType amountToAdd)
        {
            SetupContainer(out var container, out var stackable, amountToAdd);

            var possibleToAdd = (uint)stackable.Item.StackLimit - stackable.Amount;
            container.AddAtPosition(Vector2Int.zero, stackable);

            var remaining = container.AddAtPosition(Vector2Int.zero, new Package(stackable.Item, (uint)amountToAdd));

            Assert.AreEqual(remaining.Item, stackable.Item);
            Assert.AreEqual((uint)amountToAdd - possibleToAdd, remaining.Amount);

            Release(stackable.Item);
            Release(remaining.Item);
        }

        [Test]
        public void CanSwapWithFullStacks([Values(ItemStackType.Single, ItemStackType.StackOfTen, ItemStackType.StackOfHundred)] ItemStackType amountToAdd)
        {
            SetupContainer(out var container, out var package);

            package.Item.SetStackLimit(amountToAdd);
            container.AddAtPosition(Vector2Int.zero, new Package(package.Item, (uint)amountToAdd));

            var returned = container.AddAtPosition(Vector2Int.zero, package).Amount;

            Assert.AreEqual(amountToAdd, returned);
            Release(package.Item);
        }

        [Test]
        public void CanSwapItems()
        {
            SetupContainer(out var container, out var package);
            SetupOtherItem(out var other);

            container.AddToContainer(package);
            container.AddToContainer(other);

            var packageToMove = container.RemoveItemAtPosition(Vector2Int.zero, package);
            var returned = container.AddAtPosition(new(0, 1), packageToMove);
            var previous = container.AddAtPosition(Vector2Int.zero, returned);

            Assert.AreEqual(0, previous.Amount);

            Release(package.Item);
            Release(other.Item);
        }

        /// Remove Tests

        [Test]
        public void CanRemoveItemsFromContainer()
        {
            SetupContainer(out var container, out var package);

            container.AddToContainer(package);

            container.RemoveFromContainer(package);

            Assert.IsTrue(!container.storedPackages.ContainsValue(package));
            Release(package.Item);
        }

        [Test]
        public void CanRemoveFromStack()
        {
            uint stackableAmount = 10;
            uint amountToRemove = 3;
            SetupContainer(out var container, out var stackable, ItemStackType.StackOfTen, stackableAmount);

            container.AddToContainer(stackable);

            container.RemoveFromContainer(new Package(stackable.Item, amountToRemove));

            Assert.AreEqual(stackableAmount - amountToRemove, container.storedPackages[Vector2Int.zero].Amount);
            Release(stackable.Item);
        }

        [Test]
        public void CanRemoveFromMultipleStacks()
        {
            var stacklimit = ItemStackType.StackOfTen;
            SetupContainer(out var container, out var stackable, stacklimit);

            container.AddToContainer(new Package(stackable.Item, (uint)stacklimit * 3));

            container.RemoveFromContainer(new Package(stackable.Item, (uint)stacklimit * 2));

            Assert.AreEqual(1, container.storedPackages.Values.Count);
            Release(stackable.Item);
        }

        // return tests

        [Test]
        public void ReturnOriginalWhenAddingToFullContainer()
        {
            SetupContainer(out var container, out var package);
            SetupOtherItem(out var other);

            for (var i = 0; i < container.Capacity; i++)
                container.AddToContainer(package);

            var remaining = container.AddToContainer(other);

            Assert.AreEqual(other, remaining);

            Release(package.Item);
            Release(other.Item);
            Release(remaining.Item);
        }

        [Test]
        public void ReturnPreviousWhenAddingToOccupiedSlot()
        {
            SetupContainer(out var container, out var package);
            SetupOtherItem(out var other);

            container.AddToContainer(package);
            var previous = container.AddAtPosition(Vector2Int.zero, other);

            Assert.AreEqual(package, previous);

            Release(package.Item);
            Release(other.Item);
            Release(previous.Item);
        }

        [Test]
        public void ReturnZeroWhenAddingZero()
        {
            SetupContainer(out var container, out var package, ItemStackType.Single, 0);

            var remaining = container.AddToContainer(package);

            Assert.AreEqual(package, remaining);

            Release(package.Item);
            Release(remaining.Item);
        }

        [Test]
        public void ReturnZeroWhenAddingToEmpty()
        {
            SetupContainer(out var container, out var package);

            var remaining = container.AddToContainer(package);

            Assert.AreEqual(new Package(package.Item, 0), remaining);

            Release(package.Item);
            Release(remaining.Item);
        }

        [Test]
        public void ReturnRemainingWhenAddingToOpenedStack([Values(1u, 4u, 5u, 10u)] uint amount)
        {
            SetupContainer(out var container, out var stackable, ItemStackType.StackOfTen);
            var possibleToAdd = (uint)stackable.Item.StackLimit - stackable.Amount;
            var expectedAmount = amount <= possibleToAdd ? 0 : amount - possibleToAdd;

            container.AddToContainer(stackable);

            var remaining = container.AddAtPosition(Vector2Int.zero, new Package(stackable.Item, amount));

            Assert.AreEqual(expectedAmount, remaining.Amount);

            Release(stackable.Item);
            Release(remaining.Item);
        }

        [Test]
        public void ReturnStackWhenAddingToFullStack()
        {
            var stackLimit = ItemStackType.StackOfTen;
            SetupContainer(out var container, out var stackable, stackLimit);

            container.AddToContainer(new Package(stackable.Item, (uint)stackLimit));

            var remaining = container.AddAtPosition(Vector2Int.zero, stackable);

            Assert.AreEqual(stackLimit, remaining.Amount);
            Assert.AreEqual(1, container.storedPackages[Vector2Int.zero].Amount);

            Release(stackable.Item);
            Release(remaining.Item);
        }

        [Test]
        public void ReturnZetoWhenRemovingLastItemFromStack()
        {
            var stacklimit = ItemStackType.StackOfTen;
            SetupContainer(out var container, out var stackable, stacklimit);

            container.AddToContainer(stackable);

            var remaining = container.RemoveItemAtPosition(Vector2Int.zero, stackable);

            Assert.AreEqual(0, remaining.Amount);
            Release(stackable.Item);
            Release(remaining.Item);
        }

        // return 0 when removing the last item from a stack
        //


        // TODO:
        // test OccupiedSlots (adding and removing)
        // test move items between containers
    }
}
