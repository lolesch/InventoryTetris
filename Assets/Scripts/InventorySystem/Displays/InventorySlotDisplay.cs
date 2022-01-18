using System.Runtime.CompilerServices;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TeppichsTools.Logging;

[assembly: InternalsVisibleTo("Tests")]

namespace ToolSmiths.InventorySystem.Displays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]
    public class InventorySlotDisplay : MonoBehaviour, IDragAndDropDisplay, IPointerClickHandler
    {
        [SerializeField] private RectTransform itemDisplay;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI amount;

        public AbstractContainer Container;
        public Vector2Int Position;

        private static Package package;
        private static InventorySlotDisplay packageOrigin;
        private static InventorySlotDisplay dragDisplay;
        private static bool isPickedUp;
        private static bool isRemaining;
        private static Canvas canvas;

        private CanvasGroup canvasGroup;
        private GridLayoutGroup gridLayout;

        void Awake()
        {
            canvasGroup = GetComponentInChildren<CanvasGroup>();
            if (!transform.root.TryGetComponent(out canvas))
                EditorDebug.LogError($"{nameof(InventorySlotDisplay)} has no root canvas");
        }

        void Update()
        {
            if (isPickedUp)
                MoveDragDisplay();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isPickedUp)
            {
                if (Container.OccupiedSlots.TryGetValue(Position, out var packagePosition))
                {
                    PickUpItem(packagePosition);

                    isPickedUp = true;
                }
            }
            else DropItem();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Container.OccupiedSlots.TryGetValue(Position, out var packagePosition))
                PickUpItem(packagePosition);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragDisplay)
                MoveDragDisplay();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            //canvasGroup.alpha = 1;
            //canvasGroup.blocksRaycasts = true;
            //
            //dragDisplay.gameObject.SetActive(false);
        }

        public void OnDrop(PointerEventData eventData) => DropItem();

        private void PickUpItem(Vector2Int packagePosition)
        {
            packageOrigin = this;
            package = Container.StoredPackages[packagePosition];

            RefreshDragDisplay();

            canvasGroup.alpha = .3f;
            canvasGroup.blocksRaycasts = false;
        }

        private void DropItem()
        {
            // raycast through left top corner of drag display to get the right slot?

            if (packageOrigin.Container.OccupiedSlots.TryGetValue(packageOrigin.Position, out var originPackagePosition) && !isRemaining)
                packageOrigin.Container.RemoveItemAtPosition(originPackagePosition, package);

            Package remaining;

            remaining = Container.AddAtPosition(Position, package);

            package = new Package();

            if (0 < remaining.Amount && !isRemaining)
                remaining = packageOrigin.Container.AddAtPosition(originPackagePosition, remaining);

            Container.InvokeRefresh();
            packageOrigin.Container.InvokeRefresh();

            if (0 < remaining.Amount)
            {
                package = remaining;
                RefreshDragDisplay();
                isRemaining = true;
            }
            else
            {
                dragDisplay.gameObject.SetActive(false);
                packageOrigin.canvasGroup.alpha = 1;
                canvasGroup.alpha = 1;
                isPickedUp = false;
                isRemaining = false;
            }
        }

        internal void RefreshItemDisplay(Package package)
        {
            if (itemDisplay)
            {
                if (null == package.Item || package.Amount < 1)
                    itemDisplay.gameObject.SetActive(false);
                else
                {
                    SetDisplaySize(itemDisplay, package);

                    if (icon)
                        icon.sprite = package.Item.Icon;

                    if (amount)
                        amount.text = package.Amount != 1 ? package.Amount.ToString() : string.Empty;

                    itemDisplay.gameObject.SetActive(true);
                    //EditorDebug.LogWarning($"Refreshing ItemDisplay at {Position}");
                }
            }
        }

        private void SetDisplaySize(RectTransform display, Package package)
        {
            if (!gridLayout)
                gridLayout = GetComponentInParent<GridLayoutGroup>();

            Vector2 additionalSpacing = gridLayout.spacing * new Vector2(package.Item.Dimensions.x - 1, package.Item.Dimensions.y - 1);

            display.sizeDelta = gridLayout.cellSize * package.Item.Dimensions + additionalSpacing;

            display.anchoredPosition = new Vector2(display.sizeDelta.x * .5f, display.sizeDelta.y * -.5f);
            display.pivot = new Vector2(.5f, .5f);
            display.anchorMin = new Vector2(0, 1);
            display.anchorMax = new Vector2(0, 1);
        }

        private void RefreshDragDisplay()
        {
            if (null == dragDisplay)
            {
                dragDisplay = Instantiate(this, transform.root);
                dragDisplay.itemDisplay.gameObject.SetActive(true);
                dragDisplay.name = "StaticDragDisplay";

                if (dragDisplay.itemDisplay.TryGetComponent(out CanvasGroup canvasGroup))
                {
                    canvasGroup.alpha = 1;
                    canvasGroup.blocksRaycasts = false;
                }
            }

            if (null == package.Item || package.Amount < 1)
                dragDisplay.gameObject.SetActive(false);
            else
            {
                SetDisplaySize(dragDisplay.itemDisplay, package);

                if (dragDisplay.icon)
                    dragDisplay.icon.sprite = package.Item.Icon;

                if (dragDisplay.amount)
                    dragDisplay.amount.text = package.Amount != 1 ? package.Amount.ToString() : string.Empty;

                // align with mousePosition coordinates
                dragDisplay.itemDisplay.anchorMin = Vector2.zero;
                dragDisplay.itemDisplay.anchorMax = Vector2.zero;
                dragDisplay.itemDisplay.anchoredPosition = Input.mousePosition / canvas.scaleFactor;

                dragDisplay.gameObject.SetActive(true);
                EditorDebug.LogWarning($"Refreshing DragDisplay");
            }
        }

        private static void MoveDragDisplay() => dragDisplay.itemDisplay.anchoredPosition = Input.mousePosition / canvas.scaleFactor;
    }

    public interface IDragAndDropDisplay : IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler { }
}
