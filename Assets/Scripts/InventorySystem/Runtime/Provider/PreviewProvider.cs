using System.Collections.Generic;
using Submodules.Utility.Provider;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.GUI;
using ToolSmiths.InventorySystem.GUI.Displays;
using ToolSmiths.InventorySystem.GUI.InventoryDisplays;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using Submodules.Utility.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Provider
{
    /// <summary>
    /// An <see cref="AbstractSceneSingleton{T}"/>, not a full <see cref="AbstractProvider{T}"/>:
    /// it reads <c>transform.root</c> for the Canvas its own hover/compare displays render
    /// under, so it must stay nested under the HUD Canvas exactly like <see cref="DragProvider"/>
    /// - see that class's doc comment for why a provider that cannot function outside its
    /// authored parent does not promise cross-scene persistence.
    /// </summary>
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]
    internal sealed class PreviewProvider : AbstractSceneSingleton<PreviewProvider>
    {
        [SerializeField] private PreviewDisplay hoveredItem;
        private RectTransform hoveredItemTransform;
        [SerializeField] private RectTransform compareItemParent;
        [SerializeField] private PreviewDisplay compareDisplay1;
        [SerializeField] private PreviewDisplay compareDisplay2;

        private Canvas rootCanvas;
        private bool showLeft;

        // What the tooltip last showed, so a Shift press or release while it is up can re-run it.
        private Package lastPackage;
        private AbstractSlotDisplay lastSlot;
        private bool lastSecondSlot;
        private bool lastAlt;

        private float OffsetX => showLeft ? +10 : -10;

        private void Awake()
        {
            transform.root.TryGetComponent(out rootCanvas);

            hoveredItemTransform = hoveredItem.transform as RectTransform;
            hoveredItemTransform.anchorMin = Vector2.zero;
            hoveredItemTransform.anchorMax = Vector2.zero;
        }

        private void Update()
        {
            if (!hoveredItem.IsCollapsed)
            {
                MoveDisplay();

                // The compare order depends on Shift, and the roll range and comparison trade places on Alt, but
                // the tooltip is only built on hover.
                if (lastPackage.IsValid && (Input.GetKey(KeyCode.LeftShift) != lastSecondSlot || ModifierKeys.Alt != lastAlt))
                    RefreshPreviewDisplay(lastPackage, lastSlot);
            }

            void MoveDisplay()
            {
                var mousePos = Input.mousePosition / rootCanvas.scaleFactor;
                hoveredItemTransform.anchoredPosition = new Vector2(mousePos.x + OffsetX, mousePos.y);
            }
        }

        public void RefreshPreviewDisplay(Package package, AbstractSlotDisplay slot)
        {
            /// pivot pointing towards center of screen
            showLeft = Input.mousePosition.x < (Screen.width * 0.5);
            var pivotX = showLeft ? 0 : 1;
            var pivotY = Input.mousePosition.y.MapTo01(0, Screen.height);
            hoveredItemTransform.pivot = new Vector2(pivotX, pivotY);

            var mousePos = Input.mousePosition / rootCanvas.scaleFactor;
            hoveredItemTransform.anchoredPosition = new Vector2(mousePos.x + OffsetX, mousePos.y);

            lastPackage = package;
            lastSlot = slot;
            lastSecondSlot = Input.GetKey(KeyCode.LeftShift);
            lastAlt = ModifierKeys.Alt;

            if (slot is EquipmentSlotDisplay)
                hoveredItem.Refresh(package);
            else
            {
                // Shift = the second slot, the same key that equips there (InventorySlotDisplay): its
                // occupant leads the compare panels, and an equip there is what the hovered stats measure.
                IReadOnlyList<Package> shown = System.Array.Empty<Package>();
                IReadOnlyList<Package> against = null;

                if (package.Item != null && ItemService.Instance.View(package.Item).Definition.Category == ItemCategory.Equipment)
                    (shown, against) = Session.Instance.Hero.Equipment.CompareTargets(package.Item, lastSecondSlot);

                var priceOverride = slot is VendorSlotDisplay && package.Item != null
                    ? VendorTransaction.BuyPrice(package.Item, ItemService.Instance.Catalog) * package.Amount
                    : -1f;

                // Only the hovered item carries the difference; the worn items are shown as they are.
                hoveredItem.Refresh(package, against, priceOverride);

                // A worn item the equip would not displace (the bow beside a free off-hand) is only listed.
                var displaced = against ?? System.Array.Empty<Package>();
                compareDisplay1.RefreshWorn(0 < shown.Count ? shown[0] : default, package, 0 < shown.Count && IsDisplaced(shown[0]));
                compareDisplay2.RefreshWorn(1 < shown.Count ? shown[1] : default, package, 1 < shown.Count && IsDisplaced(shown[1]));

                bool IsDisplaced(Package worn)
                {
                    foreach (var other in displaced)
                        if (other.Item == worn.Item)
                            return true;

                    return false;
                }

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
