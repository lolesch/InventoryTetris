using TeppichsTools.Creation;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Displays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]
    public class StaticDragDisplay : MonoSingleton<StaticDragDisplay>//, IPointerClickHandler, IEndDragHandler
    {
        [SerializeField, ReadOnly] private Vector2Int hoveredPosition;

        public bool IsDragging => itemDisplay.gameObject.activeSelf;
        //public bool HasRemainingPackage;

        [SerializeField] private RectTransform itemDisplay;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI amount;

        private Canvas rootCanvas;

        public AbstractSlotDisplay Origin;
        public Package Package;

        private void Awake()
        {
            name = "StaticDragDisplay";

            transform.root.TryGetComponent(out rootCanvas);

            itemDisplay.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (IsDragging)
                MoveDragDisplay();

            void MoveDragDisplay()
            {
                /// anchor to BottomLeft to match screen/mouse coordinates
                itemDisplay.anchorMin = Vector2.zero;
                itemDisplay.anchorMax = Vector2.zero;

                itemDisplay.anchoredPosition = Input.mousePosition / rootCanvas.scaleFactor;
            }
        }

        public void SetPackage(AbstractSlotDisplay origin, Package package)
        {
            if (package.Item == null || package.Amount <= 0)
            {
                itemDisplay.gameObject.SetActive(false);
                return;
            }

            Origin = origin;
            Package = package;

            SetHoveredSlot(Origin);

            RefreshDisplay(Package);

            void RefreshDisplay(Package package)
            {
                SetPosition(package);

                if (icon)
                    icon.sprite = package.Item.Icon;

                if (amount)
                    amount.text = 1 < package.Amount ? package.Amount.ToString() : string.Empty;

                itemDisplay.gameObject.SetActive(true);

                void SetPosition(Package package)
                {
                    itemDisplay.sizeDelta = new Vector2(60, 60) * package.Item.Dimensions;

                    /// anchor to TopLeft to match inventoryDimension coordinates
                    itemDisplay.anchorMin = new Vector2(0, 1);
                    itemDisplay.anchorMax = new Vector2(0, 1);

                    /// get the mouse position relative to the item topLeft
                    var offset = new Vector2(.5f, .5f);
                    // TODO: continue here
                    /* SlotOffset is origin.transform.position (pivot(.5,.5)) - Input.mousePosition / rootCanvas.scaleFactor;
                     * ItemOffset = SlotOffset + CalculateOffsetBasedOnHoveredSlotPosition
                     * => get the items position - hoveredSlot position * new Vector2(60, 60)
                     */
                    itemDisplay.pivot = offset;

                    itemDisplay.anchoredPosition = new Vector2(itemDisplay.sizeDelta.x * .5f, itemDisplay.sizeDelta.y * .5f);

                    // slightly offset this to simulate pickup OR do this by color change
                    // move the Display relative to this position


                    /// Do slotHighlighting here
                    //var positionOffset = Package.Item.Dimensions / 2;
                    //Vector2 mousePositionOffset = (Input.mousePosition - transform.position) / transform.root.GetComponent<Canvas>().scaleFactor;
                    //var relativeMouseOffset = (mousePositionOffset - (transform as RectTransform).rect.size / 2) / (transform as RectTransform).rect.size;
                    //var mouseOffset = new Vector2Int(Mathf.CeilToInt(relativeMouseOffset.x), -Mathf.CeilToInt(relativeMouseOffset.y)); /// relative to topLeft => "-" to match screenCoordinates

                    //var positionToAdd = position - positionOffset + mouseOffset;

                    //if (container.CanAddAtPosition(positionToAdd, Package.Item.Dimensions, out var otherItems))
                    //{
                    //    Package remaining;
                    //
                    //    remaining = container.AddAtPosition(positionToAdd, packageToMove);
                    //
                    //    if (0 < remaining.Amount)
                    //    {
                    //        packageToMove = remaining;
                    //        StaticDragDisplay.Instance.SetPackage(this, remaining);
                    //    }
                    //    else
                    //    {
                    //        packageToMove = new Package();
                    //
                    //        StaticDragDisplay.Instance.SetPackage(this, packageToMove);
                    //    }
                    //
                    //    container.InvokeRefresh();
                    //    StaticDragDisplay.Instance.Origin.container.InvokeRefresh();
                    //}
                }
            }
        }

        public void SetHoveredSlot(AbstractSlotDisplay hoveredSlot) => hoveredPosition = hoveredSlot != null ? hoveredSlot.Position : new(-1, -1);

        //public void ReturnToOrigin(Package package)
        //{
        //    // tell teh origin to add this package back to its position
        //}

        //public void DropHere()
        //{
        //    // tell the origin to remove the package
        //    //packageOrigin.container.RemoveItemAtPosition(packageOrigin.Position, Package);
        //    // then tell the target to add the package
        //}

        //public void OnEndDrag(PointerEventData eventData) =>
        //    // raycast through center top position of drag display to check if over slotDisplay to add at, or to revert, or to drop item at floor
        //    throw new System.NotImplementedException();

        //public void OnPointerClick(PointerEventData eventData) =>
        //    // raycast through center top position of drag display to check if over slotDisplay to add at, or to revert, or to drop item at floor
        //    throw new System.NotImplementedException();
    }
}
