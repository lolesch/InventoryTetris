using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Items;
using ToolSmiths.InventorySystem.GUI.Displays;
using ToolSmiths.InventorySystem.GUI.Panels;
using ToolSmiths.InventorySystem.Runtime.Inventories;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Provider
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]
    public class PreviewProvider : AbstractProvider<PreviewProvider>
    {
        [field: SerializeField, Range(0f, 1f)] public float FadeInDelay { get; private set; } = .5f;

        [SerializeField] private ItemDetailsDisplay previewDisplay;
        [SerializeField] private SimplePanel previewPanel;
        private RectTransform previewTransform;

        //[SerializeField] private SimplePanel comparePanel1;
        //[SerializeField] private SimplePanel comparePanel2;
        [SerializeField] private RectTransform compareTransform;
        [SerializeField] private ItemDetailsDisplay compareDisplay1;
        [SerializeField] private ItemDetailsDisplay compareDisplay2;

        private bool showLeft;

        private float OffsetX => showLeft ? +10 : -10;

        private void Awake()
        {
            previewTransform = previewPanel.transform as RectTransform;
            previewTransform.anchorMin = Vector2.zero;
            previewTransform.anchorMax = Vector2.zero;

            //compareTransform = comparePanel1.transform as RectTransform;

            previewPanel.FadeOut();
            //comparePanel1.FadeOut();
            //comparePanel2.FadeOut();
        }

        private void Update()
        {
            // should this be a movable panel class that internaly handles setting the position?
            if (previewPanel.IsActive)
                previewTransform.anchoredPosition = (Vector2)Input.mousePosition / previewTransform.lossyScale + new Vector2(OffsetX, 0f);
        }

        public void CompareDetails(Package hoveredPackage, bool compareToEquipment)
        {
            if (!hoveredPackage.IsValid)
            {
                previewPanel.FadeOut();
                return;
            }

            /// pivot pointing towards center of screen
            showLeft = Input.mousePosition.x < (Screen.width * 0.5);
            var pivotX = showLeft ? 0 : 1;
            var pivotY = Input.mousePosition.y.MapTo01(0, Screen.height);
            previewTransform.pivot = new Vector2(pivotX, pivotY);

            //previewTransform.anchoredPosition = (Vector2)Input.mousePosition / previewTransform.lossyScale + new Vector2(OffsetX, 0f);

            //comparePanel1.FadeOut(0);
            //comparePanel2.FadeOut(0);

            compareDisplay1.gameObject.SetActive(false);
            compareDisplay2.gameObject.SetActive(false);

            if (hoveredPackage.Item is not EquipmentItem || !compareToEquipment)
                previewDisplay.RefreshDisplay(hoveredPackage);
            else
            {
                var equippedItems = new List<Package>();

                var possiblePositions = CharacterEquipment.GetTypeSpecificPositions((hoveredPackage.Item as EquipmentItem).EquipmentType);

                var equipmentType = (hoveredPackage.Item as EquipmentItem).EquipmentType;
                var dimensions = CharacterEquipment.IsTwoHandedWeapon(equipmentType)
                    ? new Vector2Int(2, 1)
                    : new Vector2Int(1, 1);

                var occupiedPositions = InventoryProvider.Instance.Equipment.GetStoredItemsAt(possiblePositions, dimensions);

                for (var i = 0; i < occupiedPositions.Count; i++)
                    if (InventoryProvider.Instance.Equipment.StoredPackages.TryGetValue(occupiedPositions[i], out var other))
                        equippedItems.Add(other);

                // foreach (var position in possiblePositions)
                // {
                //     var occupiedPositions = InventoryProvider.Instance.Equipment.GetStoredItemsAt(position, dimensions);
                //
                //     for (var i = 0; i < occupiedPositions.Count; i++)
                //         InventoryProvider.Instance.Equipment.StoredPackages.TryGetValue(occupiedPositions[i], out equippedItems[i]);
                // }

                if (equippedItems.Count == 0)
                    previewDisplay.RefreshDisplay(hoveredPackage);
                else if (equippedItems.Count == 1)
                {
                    previewDisplay.RefreshDisplay((hoveredPackage, equippedItems[0].Item.Affixes));

                    if (equippedItems[0].IsValid)
                    {
                        compareDisplay1.RefreshDisplay((equippedItems[0], hoveredPackage.Item.Affixes));
                        compareDisplay1.gameObject.SetActive(true);
                        //comparePanel1.FadeIn();
                    }
                }
                else if (equippedItems.Count == 2)
                {
                    if (possiblePositions.Length == 2)
                    {
                        var index = Input.GetKey(KeyCode.LeftControl) && equippedItems[1].IsValid ? 1 : 0;
                        previewDisplay.RefreshDisplay((hoveredPackage, equippedItems[index].Item.Affixes));
                    }
                    else if (possiblePositions.Length == 1)
                    {
                        var combinedAffixes = AbstractItem.CombineAffixesOfSameType(equippedItems[0].Item.Affixes.Concat(equippedItems[1].Item.Affixes).ToList());
                        previewDisplay.RefreshDisplay((hoveredPackage, combinedAffixes));
                    }

                    if (equippedItems[0].IsValid)
                    {
                        compareDisplay1.RefreshDisplay((equippedItems[0], hoveredPackage.Item.Affixes));
                        compareDisplay1.gameObject.SetActive(true);
                        //comparePanel1.FadeIn();

                        if (equippedItems[1].IsValid)
                        {
                            compareDisplay2.RefreshDisplay((equippedItems[1], hoveredPackage.Item.Affixes));
                            compareDisplay2.gameObject.SetActive(true);
                            //comparePanel2.FadeIn();
                        }
                    }
                    else if (equippedItems[1].IsValid)
                    {
                        compareDisplay1.RefreshDisplay((equippedItems[1], hoveredPackage.Item.Affixes));
                        compareDisplay1.gameObject.SetActive(true);
                        //comparePanel1.FadeIn();
                    }
                }

                (compareDisplay1.transform as RectTransform).pivot = showLeft ? Vector2.up : Vector2.one;
                (compareDisplay2.transform as RectTransform).pivot = showLeft ? Vector2.up : Vector2.one;

                var showTop = Input.mousePosition.y < (Screen.height * 0.5);
                var compPivotY = showTop ? 0 : 1;

                compareTransform.GetComponent<VerticalLayoutGroup>().childAlignment = showTop
                    ? (showLeft ? TextAnchor.LowerRight
                                : TextAnchor.LowerLeft)
                    : (showLeft ? TextAnchor.UpperRight
                                : TextAnchor.UpperLeft);

                compareTransform.pivot = new Vector2(pivotX, compPivotY);

                compareTransform.anchorMin = new Vector2(showLeft ? 1 : 0, showTop ? 0 : 1);
                compareTransform.anchorMax = new Vector2(showLeft ? 1 : 0, showTop ? 0 : 1);

                compareTransform.anchoredPosition = new Vector2(OffsetX, 0);
            }

            previewPanel.FadeIn();
        }
    }
}
