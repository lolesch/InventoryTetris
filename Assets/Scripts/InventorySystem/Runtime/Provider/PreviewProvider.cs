using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.GUI.Displays;
using ToolSmiths.InventorySystem.GUI.InventoryDisplays;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Provider
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]
    public class PreviewProvider : AbstractProvider<PreviewProvider>
    {
        [SerializeField] private PreviewDisplay hoveredDisplay;
        private RectTransform hoveredItemTransform;
        [SerializeField] private RectTransform compareItemParent;
        [SerializeField] private PreviewDisplay compareDisplay1;
        [SerializeField] private PreviewDisplay compareDisplay2;

        private Canvas rootCanvas;
        private bool showLeft;

        private float OffsetX => showLeft ? +10 : -10;

        private void Awake()
        {
            transform.root.TryGetComponent(out rootCanvas);

            hoveredItemTransform = hoveredDisplay.transform as RectTransform;
            hoveredItemTransform.anchorMin = Vector2.zero;
            hoveredItemTransform.anchorMax = Vector2.zero;
        }

        private void Update()
        {
            if (hoveredDisplay.IsPreviewing)
                MoveDisplay();

            void MoveDisplay()
            {
                var mousePos = Input.mousePosition / rootCanvas.scaleFactor;
                hoveredItemTransform.anchoredPosition = new Vector2(mousePos.x + OffsetX, mousePos.y);
            }
        }

        public void RefreshPreviewDisplay(Package hoveredPackage, AbstractSlotDisplay hoveredSlot)
        {
            /// pivot pointing towards center of screen
            showLeft = Input.mousePosition.x < (Screen.width * 0.5);
            var pivotX = showLeft ? 0 : 1;
            var pivotY = Input.mousePosition.y.MapTo01(0, Screen.height);
            hoveredItemTransform.pivot = new Vector2(pivotX, pivotY);

            var mousePos = Input.mousePosition / rootCanvas.scaleFactor;
            hoveredItemTransform.anchoredPosition = new Vector2(mousePos.x + OffsetX, mousePos.y);

            if (hoveredSlot is EquipmentSlotDisplay)
                hoveredDisplay.RefreshDisplay((hoveredPackage, new Package()));
            else
            {
                var equippedItems = new Package[2];

                if (hoveredPackage.Item is EquipmentItem)
                {
                    var equipmentPositions = CharacterEquipment.GetTypeSpecificPositions((hoveredPackage.Item as EquipmentItem).EquipmentType);

                    for (var i = 0; i < equipmentPositions.Length; i++)
                        if (!InventoryProvider.Instance.Equipment.StoredPackages.TryGetValue(equipmentPositions[i], out equippedItems[i]))
                            equippedItems[i] = new Package();
                }

                var index = Input.GetKey(KeyCode.LeftControl) ? 1 : 0;

                hoveredDisplay.RefreshDisplay((hoveredPackage, equippedItems[index]));
                compareDisplay1.RefreshDisplay((equippedItems[0], hoveredPackage));
                compareDisplay2.RefreshDisplay((equippedItems[1], hoveredPackage));

                (compareDisplay1.transform as RectTransform).pivot = showLeft ? Vector2.up : Vector2.one;
                (compareDisplay2.transform as RectTransform).pivot = showLeft ? Vector2.up : Vector2.one;

                var showTop = Input.mousePosition.y < (Screen.height * 0.5);
                var compPivotY = showTop ? 0 : 1;

                compareItemParent.GetComponent<VerticalLayoutGroup>().childAlignment = showTop
                    ? (showLeft ? TextAnchor.LowerRight
                                : TextAnchor.LowerLeft)
                    : (showLeft ? TextAnchor.UpperRight
                                : TextAnchor.UpperLeft);

                compareItemParent.pivot = new Vector2(pivotX, compPivotY);

                compareItemParent.anchorMin = new Vector2(showLeft ? 1 : 0, showTop ? 0 : 1);
                compareItemParent.anchorMax = new Vector2(showLeft ? 1 : 0, showTop ? 0 : 1);

                compareItemParent.anchoredPosition = new Vector2(OffsetX, 0);
            }
        }
    }
}
