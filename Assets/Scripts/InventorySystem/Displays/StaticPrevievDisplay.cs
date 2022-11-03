using TeppichsTools.Creation;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Displays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]
    public class StaticPrevievDisplay : MonoSingleton<StaticPrevievDisplay>
    {
        public bool IsPreviewing => itemDisplay.gameObject.activeSelf;

        [SerializeField] private RectTransform itemDisplay;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI amount;

        private Canvas rootCanvas;

        private void Awake()
        {
            name = "StaticPreviewDisplay";

            transform.root.TryGetComponent(out rootCanvas);

            itemDisplay.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (IsPreviewing)
                MoveDragDisplay();
        }

        private void MoveDragDisplay() => itemDisplay.anchoredPosition = Input.mousePosition / rootCanvas.scaleFactor;

        private void RefreshDragDisplay(Package package)
        {
            /* AbstractSlotLogic: 
             * hovering the slot for an amount of time will call the SetPackage() => enabeling this display
             * onPointerExit it should reset the package to null => disabling this display
            /* 
             * DisplayLogic:
             * The display has a Frame/Background colored in the items rarity
             * Show the items 
             *  name
             *  icon
             *  itemType
             *  list of itemStats
             *  if (it is stackable)
             *      amount/StackLimit
             *  itemValue / sellValue
             *  durability?
             *  flavor text?
             *  
             *  All these need to be set when setting the package
             */

            if (package.Amount < 1)
            {
                itemDisplay.gameObject.SetActive(false);
                return;
            }

            SetDisplay(package);

            /// align with mousePosition coordinates
            itemDisplay.anchorMin = Vector2.zero;
            itemDisplay.anchorMax = Vector2.zero;
            itemDisplay.anchoredPosition = Input.mousePosition / rootCanvas.scaleFactor;

            itemDisplay.gameObject.SetActive(true);

            void SetDisplay(Package package)
            {
                //SetDisplaySize(package);

                if (icon)
                    icon.sprite = package.Item.Icon;

                if (amount)
                    amount.text = 1 < package.Amount ? $"{package.Amount}/{package.Item.StackLimit}" : string.Empty;

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

        public void SetPackage(Package package) => RefreshDragDisplay(package);
    }
}
