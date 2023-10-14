using System.Runtime.CompilerServices;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[assembly: InternalsVisibleTo("Tests")]

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]
    public class InventorySlotDisplay : AbstractSlotDisplay
    {
        private GridLayoutGroup gridLayout;

        protected override void DropItem(Package package)
        {
            if (!package.IsValid)
                return;

            var positionOffset = AbstractItem.GetDimensions(DragProvider.Instance.DraggingPackage.Item.Dimensions) / 2;
            var mousePositionOffset = (Vector2)(Input.mousePosition - transform.position) / transform.lossyScale; //transform.root.GetComponent<Canvas>().scaleFactor;
            var relativeMouseOffset = (mousePositionOffset - (transform as RectTransform).rect.size / 2) / (transform as RectTransform).rect.size;
            var mouseOffset = new Vector2Int(Mathf.CeilToInt(relativeMouseOffset.x), -Mathf.CeilToInt(relativeMouseOffset.y));

            var positionToAdd = Position - positionOffset + mouseOffset;

            package = Container.AddAtPosition(positionToAdd, package);

            DragProvider.Instance.SetPackage(this, package, positionOffset);

            Container.InvokeRefresh();
            DragProvider.Instance.Origin.Container?.InvokeRefresh();

            FadeInPreview(); // TODO: see if the package should propagate to FadeInPreview
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

        protected override void MoveItem(PointerEventData eventData)
        {
            if (Container == null)
                return;

            var position = Position;

            if (Container.TryGetItemAt(ref position, out var package))
            {
                FadeOutPreview();

                #region USE ITEM
                if (eventData.button == PointerEventData.InputButton.Right)
                {
                    if (package.Item is ConsumableItem)
                    {
                        Debug.Log($"Consuming {package.Item.ToString()}");

                        _ = Container.RemoveAtPosition(position, new Package(Container, package.Item, 1)); // only consume one amount

                        return;
                    }
                    else if (package.Item is EquipmentItem)
                    {
                        _ = Container.RemoveAtPosition(position, package);

                        if (InventoryProvider.Instance.Equipment.TryAddToContainer(ref package))
                            DragProvider.Instance.SetPackage(this, package, Vector2Int.zero);
                        else
                            _ = Container.TryAddToContainer(ref package);

                        return;
                    }
                }
                #endregion USE ITEM

                // TODO: split in other amount => might want to split on dropping items
                #region SPLIT AMOUNT
                if (Input.GetKey(KeyCode.LeftControl))
                    if (2 <= package.Amount)
                        package.ReduceAmount(package.Amount / 2);
                #endregion SPLIT AMOUNT

                // TODO: trade context system
                #region QUICK MOVE ITEM
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    _ = Container.RemoveAtPosition(position, package);

                    var containerToMoveTo = Container; // rework to context based

                    if (Container == InventoryProvider.Instance.Inventory)
                        containerToMoveTo = InventoryProvider.Instance.Stash;
                    else if (containerToMoveTo == InventoryProvider.Instance.Stash)
                        containerToMoveTo = InventoryProvider.Instance.Inventory;

                    if (containerToMoveTo.TryAddToContainer(ref package))
                        DragProvider.Instance.SetPackage(this, package, Vector2Int.zero);
                    else
                        _ = Container.AddAtPosition(position, package);

                    return;
                }
                #endregion QUICK MOVE ITEM

                #region DRAG ITEM
                _ = Container.RemoveAtPosition(position, package);

                var positionOffset = Position - position;

                DragProvider.Instance.SetPackage(this, package, positionOffset);
                #endregion DRAG ITEM
            }

            FadeOutPreview();
        }
    }
}
