using TeppichsTools.Creation;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Displays;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[System.Serializable]
[RequireComponent(typeof(RectTransform))]
public class StaticDragDisplay : MonoSingleton<StaticDragDisplay>, IPointerClickHandler, IEndDragHandler
{
    public bool IsDragging => itemDisplay.gameObject.activeSelf;
    //public bool HasRemainingPackage;

    [SerializeField] private RectTransform itemDisplay;
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI amount;

    private Canvas rootCanvas;

    public AbstractSlotDisplay packageOrigin;
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
    }

    private void MoveDragDisplay() => itemDisplay.anchoredPosition = GetInputPosition();

    private Vector2 GetInputPosition()
    {
        var pos = Input.mousePosition / rootCanvas.scaleFactor;

        if (SystemInfo.deviceType == DeviceType.Handheld)
        {
            //if (Screen.orientation == ScreenOrientation.LandscapeRight)
            pos = new Vector2(pos.y, pos.x);
        }

        return pos;
    }

    private void RefreshDragDisplay()
    {
        if (Package.Amount < 1)
        {
            itemDisplay.gameObject.SetActive(false);
            return;
        }

        SetDisplay(Package);

        /// align with mousePosition coordinates
        itemDisplay.anchorMin = Vector2.zero;
        itemDisplay.anchorMax = Vector2.zero;
        itemDisplay.anchoredPosition = GetInputPosition();

        itemDisplay.gameObject.SetActive(true);

        void SetDisplay(Package package)
        {
            SetDisplaySize(package);

            if (icon)
                icon.sprite = package.Item.Icon;

            if (amount)
                amount.text = 1 < package.Amount ? package.Amount.ToString() : string.Empty;

            void SetDisplaySize(Package package)
            {
                itemDisplay.sizeDelta = new Vector2(60, 60) * package.Item.Dimensions;

                itemDisplay.anchoredPosition = new Vector2(itemDisplay.sizeDelta.x * .5f, itemDisplay.sizeDelta.y * -.5f);
                itemDisplay.pivot = new Vector2(.5f, .5f);
                itemDisplay.anchorMin = new Vector2(0, 1);
                itemDisplay.anchorMax = new Vector2(0, 1);
            }
        }
    }

    public void SetPackage(AbstractSlotDisplay origin, Package package)
    {
        packageOrigin = origin;
        this.Package = package;

        RefreshDragDisplay();
    }

    public void ReturnToOrigin(Package package)
    {
        // tell teh origin to add this package back to its position
    }

    public void DropHere()
    {
        // tell the origin to remove the package
        //packageOrigin.container.RemoveItemAtPosition(packageOrigin.Position, Package);
        // then tell the target to add the package
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // raycast through center top position of drag display to check if over slotDisplay to add at, or to revert, or to drop item at floor
        throw new System.NotImplementedException();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // raycast through center top position of drag display to check if over slotDisplay to add at, or to revert, or to drop item at floor
        throw new System.NotImplementedException();
    }

}
