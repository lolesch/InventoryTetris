using TeppichsTools.Creation;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Displays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]
    public class StaticPrevievDisplay : MonoSingleton<StaticPrevievDisplay>
    {
        // TODO: handle comparison with equipped items

        [SerializeField] private PreviewDisplay hoveredItem;
        [SerializeField] private PreviewDisplay compareItem;

        private Canvas rootCanvas;

        private void Awake() => transform.root.TryGetComponent(out rootCanvas);

        private void Update()
        {
            if (hoveredItem.IsPreviewing)
                MoveDisplay();

            void MoveDisplay() => hoveredItem.ItemDisplay.anchoredPosition = Input.mousePosition / rootCanvas.scaleFactor;
        }

        public void RefreshPreviewDisplay(Package package, AbstractSlotDisplay slot)
        {
            var compareTo = new Package(null, 0);

            if (package.Item is EquipmentItem && slot is not EquipmentSlotDisplay)
            {
                var equipmentPosition = InventoryProvider.Instance.PlayerEquipment.GetEquipmentTypePosition((package.Item as EquipmentItem).EquipmentType);

                //var currentEquipped = InventoryProvider.Instance.PlayerEquipment.GetStoredPackagesAtPosition(equipmentPosition, new(1, 1));
                InventoryProvider.Instance.PlayerEquipment.StoredPackages.TryGetValue(equipmentPosition, out compareTo);
            }

            hoveredItem.SetDisplay(package, compareTo);
            compareItem.SetDisplay(compareTo, package);

            // TODO: set pivot relative to the screen border
            // and align next to the items dimensions?
            hoveredItem.ItemDisplay.pivot = new Vector2(1, .5f);

            hoveredItem.ItemDisplay.anchorMin = Vector2.zero;
            hoveredItem.ItemDisplay.anchorMax = Vector2.zero;
            hoveredItem.ItemDisplay.anchoredPosition = Input.mousePosition / rootCanvas.scaleFactor;
        }
    }
}
