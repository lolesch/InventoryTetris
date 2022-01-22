using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests
{
    public class ContainerTest
    {
        protected static void Release(Object obj) => Object.DestroyImmediate(obj);

        private static void SetupOtherItem(out Package other, uint stackLimit = 1u, uint amount = 1u)
        {
            other = new Package(ScriptableObject.CreateInstance<Item>(), amount);
            other.Item.SetStackLimit(stackLimit);
        }

        private static void SetupContainer(out PlayerInventory container, out Package package, uint stackLimit = 1u, uint amount = 1u, int dimensionX = 5, int dimensionY = 3)
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
            SetupContainer(out PlayerInventory container, out Package package);

            uint remaining = container.AddToContainer(package).Amount;

            Assert.IsTrue(container.storedPackages.ContainsValue(package));
            Assert.IsTrue(remaining == 0);

            Release(package.Item);
        }

        [Test]
        public void CanAddToStacksBeforeEmpltyPositions()
        {
            SetupContainer(out PlayerInventory container, out Package stackable, 3, 2);

            container.AddAtPosition(new(0, 1), stackable);
            container.AddToContainer(stackable);

            Assert.AreEqual(1, container.storedPackages[new(0, 0)].Amount);
        }

        [Test]
        public void CanAddToOpenedStacks()
        {
            SetupContainer(out PlayerInventory container, out Package stackable, 2);

            container.AddToContainer(stackable);
            container.AddToContainer(stackable);

            Assert.AreEqual(1, container.storedPackages.Count);

            Release(stackable.Item);
        }

        [Test]
        public void CanAddAtPosition()
        {
            SetupContainer(out PlayerInventory container, out Package package);

            container.AddAtPosition(Vector2Int.zero, package);

            Assert.AreEqual(package, container.storedPackages[Vector2Int.zero]);
            Release(package.Item);
        }

        [Test]
        public void CanAddToMultipleOpenedStacks()
        {
            SetupContainer(out PlayerInventory container, out Package stackable, 16, 15);

            for (int x = 0; x < container.Dimensions.x; x++)
                for (int y = 0; y < container.Dimensions.y; y++)
                    container.AddAtPosition(new(x, y), stackable);

            container.AddToContainer(stackable);

            uint amount = 0;
            foreach (var package in container.storedPackages.Values)
                amount += package.Amount;

            Assert.AreEqual(container.Capacity * 16, amount);
            Release(stackable.Item);
        }

        [Test]
        public void CanAddMultipleItems()
        {
            SetupContainer(out PlayerInventory container, out Package package);

            package = new Package(package.Item, 15);
            container.AddToContainer(package);

            Assert.AreEqual(15, container.storedPackages.Count);
            Release(package.Item);
        }

        [Test]
        public void HasEmptyDimensions()
        {
            SetupContainer(out PlayerInventory container, out Package package);

            container.AddToContainer(package);

            Vector2Int position = new(-1, -1);

            for (int x = 0; x < container.Dimensions.x && 0 < package.Amount; x++)
                for (int y = 0; y < container.Dimensions.y && 0 < package.Amount; y++)
                    if (container.IsValidPosition(new(x, y), package.Item.Dimensions))
                        if (container.IsEmptyPosition(new(x, y), package.Item.Dimensions))
                            position = new(x, y);

            Assert.IsTrue(container.IsWithinDimensions(position));
            Release(package.Item);
        }

        [Test]
        public void IsFull()
        {
            SetupContainer(out PlayerInventory container, out Package package);

            for (int i = 0; i < container.Capacity; i++)
                container.AddToContainer(package);

            bool isFull = true;

            for (int x = 0; x < container.Dimensions.x && 0 < package.Amount; x++)
                for (int y = 0; y < container.Dimensions.y && 0 < package.Amount; y++)
                    if (container.IsValidPosition(new(x, y), package.Item.Dimensions))
                        if (container.IsEmptyPosition(new(x, y), package.Item.Dimensions))
                            isFull = false;

            Assert.IsTrue(isFull);
            Release(package.Item);
        }

        [Test]
        public void IsOccupiedAtPosition()
        {
            SetupContainer(out PlayerInventory container, out Package package);

            for (int i = 0; i < container.Capacity; i++)
                container.AddToContainer(package);

            int others = container.GetStoredPackagesAtPosition(Vector2Int.zero, new(2, 2)).Count;

            Assert.IsTrue(0 < others);
            Release(package.Item);
        }

        [Test]
        public void IsWithinDimensions()
        {
            SetupContainer(out PlayerInventory container, out Package package);

            bool isTooSmall = container.IsWithinDimensions(new(-1, -1));
            bool isTooBigX = container.IsWithinDimensions(new(container.Dimensions.x, 0));
            bool isTooBigY = container.IsWithinDimensions(new(0, container.Dimensions.y));

            Assert.IsTrue(!isTooSmall && !isTooBigX && !isTooBigY);
            Release(package.Item);
        }

        [Test]
        public void CanStackItems([Values(0u, 1u, 5u, 99u)] uint amountToAdd)
        {
            SetupContainer(out PlayerInventory container, out Package stackable, amountToAdd);

            uint possibleToAdd = stackable.Item.StackLimit - stackable.Amount;
            container.AddAtPosition(Vector2Int.zero, stackable);

            var remaining = container.AddAtPosition(Vector2Int.zero, new Package(stackable.Item, amountToAdd));

            Assert.AreEqual(remaining.Item, stackable.Item);
            Assert.AreEqual(amountToAdd - possibleToAdd, remaining.Amount);

            Release(stackable.Item);
            Release(remaining.Item);
        }

        [Test]
        public void CanSwapWithFullStacks([Values(1u, 5u, 99u)] uint amountToAdd)
        {
            SetupContainer(out PlayerInventory container, out Package package);

            package.Item.SetStackLimit(amountToAdd);
            container.AddAtPosition(Vector2Int.zero, new Package(package.Item, amountToAdd));

            uint returned = container.AddAtPosition(Vector2Int.zero, package).Amount;

            Assert.AreEqual(amountToAdd, returned);
            Release(package.Item);
        }

        [Test]
        public void CanSwapItems()
        {
            SetupContainer(out PlayerInventory container, out Package package);
            SetupOtherItem(out Package other);

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
            SetupContainer(out PlayerInventory container, out Package package);

            container.AddToContainer(package);

            container.RemoveFromContainer(package);

            Assert.IsTrue(!container.storedPackages.ContainsValue(package));
            Release(package.Item);
        }

        [Test]
        public void CanRemoveFromStack()
        {
            uint stackableAmount = 5;
            uint amountToRemove = 3;
            SetupContainer(out PlayerInventory container, out Package stackable, 5, stackableAmount);

            container.AddToContainer(stackable);

            container.RemoveFromContainer(new Package(stackable.Item, amountToRemove));

            Assert.AreEqual(stackableAmount - amountToRemove, container.storedPackages[Vector2Int.zero].Amount);
            Release(stackable.Item);
        }

        [Test]
        public void CanRemoveFromMultipleStacks()
        {
            uint stacklimit = 5;
            SetupContainer(out PlayerInventory container, out Package stackable, stacklimit);

            container.AddToContainer(new Package(stackable.Item, stacklimit * 3));

            container.RemoveFromContainer(new Package(stackable.Item, stacklimit * 2));

            Assert.AreEqual(1, container.storedPackages.Values.Count);
            Release(stackable.Item);
        }

        // return tests

        [Test]
        public void ReturnOriginalWhenAddingToFullContainer()
        {
            SetupContainer(out PlayerInventory container, out Package package);
            SetupOtherItem(out Package other);

            for (int i = 0; i < container.Capacity; i++)
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
            SetupContainer(out PlayerInventory container, out Package package);
            SetupOtherItem(out Package other);

            container.AddToContainer(package);
            Package previous = container.AddAtPosition(Vector2Int.zero, other);

            Assert.AreEqual(package, previous);

            Release(package.Item);
            Release(other.Item);
            Release(previous.Item);
        }

        [Test]
        public void ReturnZeroWhenAddingZero()
        {
            SetupContainer(out PlayerInventory container, out Package package, 1, 0);

            var remaining = container.AddToContainer(package);

            Assert.AreEqual(package, remaining);

            Release(package.Item);
            Release(remaining.Item);
        }

        [Test]
        public void ReturnZeroWhenAddingToEmpty()
        {
            SetupContainer(out PlayerInventory container, out Package package);

            var remaining = container.AddToContainer(package);

            Assert.AreEqual(new Package(package.Item, 0), remaining);

            Release(package.Item);
            Release(remaining.Item);
        }

        [Test]
        public void ReturnRemainingWhenAddingToOpenedStack([Values(1u, 4u, 5u, 10u)] uint amount)
        {
            SetupContainer(out PlayerInventory container, out Package stackable, 5);
            uint possibleToAdd = stackable.Item.StackLimit - stackable.Amount;
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
            uint stackLimit = 5u;
            SetupContainer(out PlayerInventory container, out Package stackable, stackLimit);

            container.AddToContainer(new Package(stackable.Item, stackLimit));

            var remaining = container.AddAtPosition(Vector2Int.zero, stackable);

            Assert.AreEqual(stackLimit, remaining.Amount);
            Assert.AreEqual(1, container.storedPackages[Vector2Int.zero].Amount);

            Release(stackable.Item);
            Release(remaining.Item);
        }

        [Test]
        public void ReturnZetoWhenRemovingLastItemFromStack()
        {
            uint stacklimit = 5;
            SetupContainer(out PlayerInventory container, out Package stackable, stacklimit);

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
