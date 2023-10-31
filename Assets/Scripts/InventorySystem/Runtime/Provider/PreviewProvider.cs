using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.GUI.Displays;
using ToolSmiths.InventorySystem.GUI.InventoryDisplays;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Provider
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]
    public class PreviewProvider : AbstractProvider<PreviewProvider>
    {
        [SerializeField] private PreviewDisplay hoveredItem;
        [SerializeField] private RectTransform compareItemParent;
        [SerializeField] private PreviewDisplay compareItem1;
        [SerializeField] private PreviewDisplay compareItem2;

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
                var equipmentPositions = CharacterEquipment.GetTypeSpecificPositions((package.Item as EquipmentItem).EquipmentType);

                for (var i = 0; i < equipmentPositions.Length; i++)
                    InventoryProvider.Instance.Equipment.StoredPackages.TryGetValue(equipmentPositions[i], out compareTo[i]);
            }

            hoveredItem.SetDisplay(package, compareTo[0]);

            /// pivot pointing towards center of screen
            showLeft = Input.mousePosition.x < (Screen.width * 0.5);

            var pivotX = showLeft ? 0 : 1;
            var pivotY = Input.mousePosition.y.MapTo01(0, Screen.height);

            hoveredItem.ItemDisplay.pivot = new Vector2(pivotX, pivotY);

            hoveredItem.ItemDisplay.anchorMin = Vector2.zero;
            hoveredItem.ItemDisplay.anchorMax = Vector2.zero;

            var mousePos = Input.mousePosition / rootCanvas.scaleFactor;
            hoveredItem.ItemDisplay.anchoredPosition = new Vector2(mousePos.x + OffsetX, mousePos.y);

            // TODO: compare to each
            compareItem1.SetDisplay(compareTo[0], package);
            compareItem2.SetDisplay(compareTo[1], package);

            compareItemParent.pivot = new Vector2(pivotX, 1);

            compareItemParent.anchorMin = showLeft ? Vector2.one : Vector2.up;
            compareItemParent.anchorMax = showLeft ? Vector2.one : Vector2.up;

            compareItemParent.anchoredPosition = new Vector2(OffsetX, 20);
        }
    }
}
