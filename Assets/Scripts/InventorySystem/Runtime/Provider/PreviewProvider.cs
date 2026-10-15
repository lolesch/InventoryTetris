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
        [SerializeField] private HorizontalOrVerticalLayoutGroup compareGroup;
        [SerializeField] private PreviewDisplay comparedItem1;
        [SerializeField] private PreviewDisplay comparedItem2;

        private RectTransform compareTransform;
        private Canvas rootCanvas;
        private bool showLeft;
        private float pivotX;

        // What the tooltip last showed, so a Shift press or release while it is up can re-run it.
        private Package lastPackage;
        private AbstractSlotDisplay lastSlot;
        private bool lastSecondSlot;
        private bool lastDetails;

        private const float OffestDisplayToMouse = 15f;
        private float OffsetX => showLeft ? OffestDisplayToMouse : -OffestDisplayToMouse;

        private void Awake()
        {
            transform.root.TryGetComponent(out rootCanvas);

            compareTransform = (RectTransform)compareGroup.transform;
            compareTransform.anchorMin = Vector2.zero;
            compareTransform.anchorMax = Vector2.zero;
        }

        private void Update()
        {
            if (hoveredItem.IsCollapsed) 
                return;
            
            var mousePos = GameInput.PointerPosition / rootCanvas.scaleFactor;
            compareTransform.anchoredPosition = new Vector2(mousePos.x + OffsetX, mousePos.y);

            // One read of each per frame: every GameInput call is a service lookup.
            var secondSlot = GameInput.IsHeld(GameAction.SecondSlot);
            var details = GameInput.IsHeld(GameAction.ShowDetails);

            if (secondSlot != lastSecondSlot || details != lastDetails)
                RefreshPreviewDisplay(lastPackage, lastSlot);
        }

        public void RefreshPreviewDisplay(Package package, AbstractSlotDisplay slot)
        {
            var pointer = GameInput.PointerPosition;

            if (!package.Equals(lastPackage))
            {
                showLeft = pointer.x < (Screen.width * 0.5);
                pivotX = showLeft ? 0 : 1;

                comparedItem1.gameObject.SetActive(false);
                comparedItem2.gameObject.SetActive(false);
            }

            lastPackage = package;
            lastSlot = slot;
            lastSecondSlot = GameInput.IsHeld(GameAction.SecondSlot);
            lastDetails = GameInput.IsHeld(GameAction.ShowDetails);

            var pivotY = pointer.y.MapTo01(0, Screen.height);
            var mousePos = pointer / rootCanvas.scaleFactor;

            compareTransform.pivot = new Vector2(pivotX, pivotY);
            compareTransform.anchoredPosition = new Vector2(mousePos.x + OffsetX, mousePos.y);

            if (slot is EquipmentSlotDisplay)
                hoveredItem.Repaint(package);
            else
            {
                // Shift = the second slot, the same key that equips there (InventorySlotDisplay).
                IReadOnlyList<Package> shown = System.Array.Empty<Package>();
                IReadOnlyList<Package> against = null;

                if (package.IsValid && 
                    ItemService.Instance.View(package.Item).Definition.Category == ItemCategory.Equipment)
                    (shown, against) = Session.Instance.Hero.Equipment.CompareTargets(package.Item, lastSecondSlot);

                // Only a shelf prices differently (the buy price); every other slot shows the stack's sell value.
                var priceOverride = slot is VendorSlotDisplay && package.IsValid
                    ? VendorTransaction.BuyPrice(package.Item, ItemService.Instance.Catalog) * package.Amount
                    : -1f;

                hoveredItem.Repaint(package, against, priceOverride);

                // A worn item the equip would not displace (the bow beside a free off-hand) is only listed.
                var displaced = against ?? System.Array.Empty<Package>();
                
                var show1 = 0 < shown.Count;
                var show2 = 1 < shown.Count;
                
                comparedItem1.gameObject.SetActive(show1);
                comparedItem1.RepaintWorn(show1 ? shown[0] : default, package, show1 && IsDisplaced(shown[0]));

                // Only a two-hander has a second worn item to list (CompareTargets).
                comparedItem2.gameObject.SetActive(show2);
                comparedItem2.RepaintWorn(show2 ? shown[1] : default, package, show2 && IsDisplaced(shown[1]));

                compareGroup.reverseArrangement = !showLeft;
                compareGroup.childAlignment = showLeft ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
                
                return;

                bool IsDisplaced(Package worn)
                {
                    foreach (var other in displaced)
                        if (other.Item == worn.Item)
                            return true;

                    return false;
                }
            }
        }
    }
}