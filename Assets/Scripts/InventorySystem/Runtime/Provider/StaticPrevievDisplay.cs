using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.GUI.Displays;
using ToolSmiths.InventorySystem.GUI.InventoryDisplays;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Provider
{
    // TODO: inherit AbstractDisplay
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]
    public class StaticPrevievDisplay : AbstractProvider<StaticPrevievDisplay>
    {
        [SerializeField] private PreviewDisplay hoveredItem;
        [SerializeField] private PreviewDisplay compareItem;

        private Canvas rootCanvas;
        private bool showLeft;

        private float OffsetX => showLeft ? +10 : -10;

        private void Awake() => transform.root.TryGetComponent(out rootCanvas);

        private void Update()
        {
            if (hoveredItem.IsPreviewing)
                MoveDisplay();

            void MoveDisplay()
            {
                var mousePos = Input.mousePosition / rootCanvas.scaleFactor;
                hoveredItem.ItemDisplay.anchoredPosition = new Vector2(mousePos.x + OffsetX, mousePos.y);
            }
        }

        public void RefreshPreviewDisplay(Package package, AbstractSlotDisplay slot)
        {
            var other = new Package(null, null, 0);
            var compareTo = new Package[2] { other, other };

            // TODO: do not compare against itself when hovering an equipment slot !
            if (package.Item is EquipmentItem && slot is not EquipmentSlotDisplay)
            {
                // TODO compare to all equipments of the items equipmentType
                var equipmentPositions = InventoryProvider.Instance.PlayerEquipment.GetTypeSpecificPositions((package.Item as EquipmentItem).EquipmentType);

                for (var i = 0; i < equipmentPositions.Length; i++)
                    InventoryProvider.Instance.PlayerEquipment.StoredPackages.TryGetValue(equipmentPositions[i], out compareTo[i]);
            }

            hoveredItem.SetDisplay(package, compareTo);

            /// pivot pointing towards center of screen
            showLeft = Input.mousePosition.x < (Screen.width * 0.5);

            var pivotX = showLeft ? 0 : 1;
            var pivotY = Input.mousePosition.y.MapTo01(0, Screen.height);

            hoveredItem.ItemDisplay.pivot = new Vector2(pivotX, pivotY);

            hoveredItem.ItemDisplay.anchorMin = Vector2.zero;
            hoveredItem.ItemDisplay.anchorMax = Vector2.zero;

            var mousePos = Input.mousePosition / rootCanvas.scaleFactor;
            hoveredItem.ItemDisplay.anchoredPosition = new Vector2(mousePos.x + OffsetX, mousePos.y);

            compareItem.SetDisplay(compareTo[0], new Package[1] { package });

            compareItem.ItemDisplay.pivot = new Vector2(pivotX, 1);

            compareItem.ItemDisplay.anchorMin = showLeft ? Vector2.one : Vector2.up;
            compareItem.ItemDisplay.anchorMax = showLeft ? Vector2.one : Vector2.up;

            compareItem.ItemDisplay.anchoredPosition = new Vector2(OffsetX, 20);
        }
    }
}
