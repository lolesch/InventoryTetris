using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public class SellItenSlotDisplay : AbstractSlotDisplay
    {
        protected override void DropItem()
        {
            if (StaticDragDisplay.Instance.Package.Item != null)
            {
                var packageToMove = StaticDragDisplay.Instance.Package;

                StaticDragDisplay.Instance.Origin.Container?.RemoveFromContainer(packageToMove);

                var currency = new Currency(packageToMove.Item.GoldValue);

                InventoryProvider.Instance.Inventory.AddToContainer(new Package(Container, ItemProvider.Instance.GenerateCurrency(Data.Enums.CurrencyType.Gold), currency.Gold));
                InventoryProvider.Instance.Inventory.AddToContainer(new Package(Container, ItemProvider.Instance.GenerateCurrency(Data.Enums.CurrencyType.Silver), currency.Silver));
                InventoryProvider.Instance.Inventory.AddToContainer(new Package(Container, ItemProvider.Instance.GenerateCurrency(Data.Enums.CurrencyType.Copper), currency.Copper));
                InventoryProvider.Instance.Inventory.AddToContainer(new Package(Container, ItemProvider.Instance.GenerateCurrency(Data.Enums.CurrencyType.Iron), currency.Iron));

                StaticDragDisplay.Instance.SetPackage(this, new Package(), Vector2Int.zero);

                Container?.InvokeRefresh();
                StaticDragDisplay.Instance.Origin.Container?.InvokeRefresh();
            }

            // must come after adding items to the container to have something to preview
            base.DropItem();
        }
    }
}
