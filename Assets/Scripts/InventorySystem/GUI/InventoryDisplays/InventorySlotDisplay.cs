using System.Runtime.CompilerServices;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;
using UnityEngine.UI;

[assembly: InternalsVisibleTo("Tests")]

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]
    public class InventorySlotDisplay : AbstractSlotDisplay
    {
        private GridLayoutGroup gridLayout;

        protected override void DropItem()
        {
            if (StaticDragDisplay.Instance.Package.Item != null)
            {
                var positionOffset = AbstractItem.GetDimensions(StaticDragDisplay.Instance.Package.Item.Dimensions) / 2;
                var mousePositionOffset = (Vector2)(Input.mousePosition - transform.position) / transform.lossyScale; //transform.root.GetComponent<Canvas>().scaleFactor;
                var relativeMouseOffset = (mousePositionOffset - (transform as RectTransform).rect.size / 2) / (transform as RectTransform).rect.size;
                var mouseOffset = new Vector2Int(Mathf.CeilToInt(relativeMouseOffset.x), -Mathf.CeilToInt(relativeMouseOffset.y));

                var positionToAdd = Position - positionOffset + mouseOffset;

                var remaining = Container.AddAtPosition(positionToAdd, packageToMove);

                if (0 < remaining.Amount)
                {
                    packageToMove = remaining;
                    StaticDragDisplay.Instance.SetPackage(this, remaining, positionOffset);
                }
                else
                {
                    packageToMove = new Package();

                    StaticDragDisplay.Instance.SetPackage(this, packageToMove, positionOffset);
                }

                Container.InvokeRefresh();
                StaticDragDisplay.Instance.Origin.Container?.InvokeRefresh();
            }

            // must come after adding items to the container to have something to preview
            base.DropItem();
        }

        protected override void SetDisplaySize(RectTransform display, Package package)
        {
            base.SetDisplaySize(display, package);

            if (!gridLayout)
                gridLayout = GetComponentInParent<GridLayoutGroup>();
            if (gridLayout)
            {
                var additionalSpacing = gridLayout.spacing * new Vector2(AbstractItem.GetDimensions(package.Item.Dimensions).x - 1, AbstractItem.GetDimensions(package.Item.Dimensions).y - 1);

                display.sizeDelta = gridLayout.cellSize * AbstractItem.GetDimensions(package.Item.Dimensions) + additionalSpacing;
            }

            display.anchoredPosition = new Vector2(display.sizeDelta.x * .5f, display.sizeDelta.y * -.5f);
            display.pivot = new Vector2(.5f, .5f);
            display.anchorMin = new Vector2(0, 1);
            display.anchorMax = new Vector2(0, 1);
        }

        protected override void EquipItem()
        {
            var storedPositions = Container.GetStoredItemsAt(Position, new(1, 1));

            if (storedPositions.Count == 1)
                if (Container.StoredPackages.TryGetValue(storedPositions[0], out packageToMove))
                {
                    if (packageToMove.Item is EquipmentItem)
                    {
                        _ = Container.RemoveAtPosition(storedPositions[0], packageToMove);

                        packageToMove = InventoryProvider.Instance.Equipment.AddToContainer(packageToMove);

                        if (0 < packageToMove.Amount)
                            packageToMove = Container.AddToEmptyPosition(packageToMove);

                        var positionOffset = Position - storedPositions[0]; // might look up the added package and get that position instead

                        StaticDragDisplay.Instance.SetPackage(this, packageToMove, positionOffset);

                        //Container.InvokeRefresh();
                        //SStaticDragDisplay.Instance.Origin.Container?.InvokeRefresh();
                    }
                }

            base.EquipItem();
        }

        protected override void ConsumeItem()
        {
            var storedPositions = Container.GetStoredItemsAt(Position, new(1, 1));

            if (storedPositions.Count == 1)
                if (Container.StoredPackages.TryGetValue(storedPositions[0], out packageToMove))
                {
                    if (packageToMove.Item is ConsumableItem)
                    {
                        Debug.Log($"Consuming {packageToMove.Item.ToString()}");

                        _ = Container.RemoveAtPosition(storedPositions[0], new Package(Container, packageToMove.Item, 1));
                    }
                }

            base.ConsumeItem();
        }
    }
}
