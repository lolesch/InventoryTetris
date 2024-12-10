using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    internal sealed class VendorSlotDisplay : AbstractSlotDisplay
    {
        private GridLayoutGroup gridLayout;

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

            if (!Container.TryGetItemAt(ref position, out var package))
                return;

            if( !InventoryProvider.Instance.Inventory.TryPay( package.Item.SellValue ) )
                return;

            FadeOutPreview();

            #region USE ITEM
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                _ = Container.RemoveAtPosition(position, package);

                if (InventoryProvider.Instance.Inventory.TryAddToContainer(ref package))
                    DragProvider.Instance.SetPackage(this, package, Vector2Int.zero);
                else
                    _ = Container.AddAtPosition(position, package);

                return;
            }
            #endregion USE ITEM

            // TODO: trade context system
            #region QUICK MOVE ITEM
            if (Input.GetKey(KeyCode.LeftShift))
            {
                _ = Container.RemoveAtPosition(position, package);

                if (InventoryProvider.Instance.Inventory.TryAddToContainer(ref package))
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

        protected override void DropItem(Package package)
        {
            if (!package.IsValid || package.Sender == Container)
                return;

            var packageToMove = DragProvider.Instance.DraggingPackage;

            _ = (DragProvider.Instance.Origin.Container?.RemoveFromContainer(packageToMove));

            var currency = new Currency(packageToMove.Item.SellValue * packageToMove.Amount);

            //TODO: handle item loss if inventory is full
            var gold = new Package(Container, ItemProvider.Instance.GenerateCurrency(Data.Enums.CurrencyType.Gold), currency.Gold);
            _ = InventoryProvider.Instance.Inventory.TryAddToContainer(ref gold);
            var silver = new Package(Container, ItemProvider.Instance.GenerateCurrency(Data.Enums.CurrencyType.Silver), currency.Silver);
            _ = InventoryProvider.Instance.Inventory.TryAddToContainer(ref silver);
            var iron = new Package(Container, ItemProvider.Instance.GenerateCurrency(Data.Enums.CurrencyType.Iron), currency.Iron);
            _ = InventoryProvider.Instance.Inventory.TryAddToContainer(ref iron);
            var copper = new Package(Container, ItemProvider.Instance.GenerateCurrency(Data.Enums.CurrencyType.Copper), currency.Copper);
            _ = InventoryProvider.Instance.Inventory.TryAddToContainer(ref copper);

            DragProvider.Instance.SetPackage(this, new Package(), Vector2Int.zero);

            Container?.InvokeRefresh();
            DragProvider.Instance.Origin.Container?.InvokeRefresh();

            FadeInPreview(); // TODO: see if the package should propagate to FadeInPreview
        }
    }
}
