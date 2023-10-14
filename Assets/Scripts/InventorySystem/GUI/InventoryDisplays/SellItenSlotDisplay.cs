using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public class SellItenSlotDisplay : AbstractSlotDisplay
    {
        protected override void DropItem(Package package)
        {
            if (!package.IsValid)
                return;

            var packageToMove = DragProvider.Instance.DraggingPackage;

            DragProvider.Instance.Origin.Container?.RemoveFromContainer(packageToMove);

            var currency = new Currency(packageToMove.Item.SellValue);

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

        protected override void MoveItem(PointerEventData eventData) { }
    }
}
