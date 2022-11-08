using System.Collections.Generic;
using TeppichsTools.Creation;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Displays
{
    [System.Serializable]
    [RequireComponent(typeof(RectTransform))]
    public class StaticPrevievDisplay : MonoSingleton<StaticPrevievDisplay>
    {
        // TODO: handle comparison with equipped items

        public bool IsPreviewing => itemDisplay.gameObject.activeSelf;

        [SerializeField] private RectTransform itemDisplay;
        [SerializeField] private Image icon;
        [SerializeField] private List<Image> frameAndHorizontalLines;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI itemName;
        [SerializeField] private TextMeshProUGUI itemType;
        [SerializeField] private TextMeshProUGUI amount;
        [SerializeField] private TextMeshProUGUI itemStatPrefab;

        public Vector2Int StoredPosition { get; private set; } = new(-1, -1);
        private Canvas rootCanvas;

        private void Awake()
        {
            name = "StaticPreviewDisplay";

            transform.root.TryGetComponent(out rootCanvas);

            itemDisplay.gameObject.SetActive(false);
        }

        //private void Update()
        //{
        //    if (IsPreviewing)
        //        MoveDisplay();
        //}
        //
        //private void MoveDisplay() => itemDisplay.anchoredPosition = Input.mousePosition / rootCanvas.scaleFactor;

        private void RefreshDragDisplay(Package package)
        {
            if (package.Amount < 1)
            {
                itemDisplay.gameObject.SetActive(false);
                return;
            }

            var equipmentPosition = InventoryProvider.Instance.PlayerEquipment.GetEquipmentTypePosition((package.Item as Equipment).equipmentType);
            //var currentEquipped = InventoryProvider.Instance.PlayerEquipment.GetStoredPackagesAtPosition(equipmentPosition, new(1, 1));
            InventoryProvider.Instance.PlayerEquipment.storedPackages.TryGetValue(equipmentPosition, out var compareTo);

            SetDisplay(package, compareTo);

            // TODO: choose on what side to display when aligning with screen border
            // and align next to the items dimensions
            itemDisplay.anchorMin = Vector2.zero;
            itemDisplay.anchorMax = Vector2.zero;
            itemDisplay.anchoredPosition = Input.mousePosition / rootCanvas.scaleFactor;

            itemDisplay.gameObject.SetActive(true);

            void SetDisplay(Package package, Package compareTo)
            {
                if (compareTo.Item != null && 0 < compareTo.Amount)
                { } // => SetCompareDisplay(compareTo, package)

                if (itemName)
                    itemName.text = package.Item.name;

                if (itemType)
                {
                    itemType.text = package.Item is Equipment ? (package.Item as Equipment).equipmentType.ToString() : string.Empty;
                    //itemType.color = rarityTypeColor;
                }

                if (icon)
                    icon.sprite = package.Item.Icon;

                if (amount)
                    amount.text = 1 < package.Amount && package.Item is Consumable ? $"{package.Amount}/{(int)(package.Item as Consumable).StackLimit}" : string.Empty;

                // TODO: how to derive rarity
                //if (frameAndHorizontalLines != null && 0 < frameAndHorizontalLines.Count)
                //    for (var i = 0; i < frameAndHorizontalLines.Count; i++)
                //        frameAndHorizontalLines[i].color = rarityFrameColor;
                //
                //if (background)
                //    background.color = rarityBackgroudColor;

                // TODO: make this poolable
                if (itemStatPrefab)
                {
                    for (var i = itemStatPrefab.transform.parent.childCount; i-- > 1;)
                    {
                        Destroy(itemStatPrefab.transform.parent.GetChild(i).gameObject);
                        DestroyImmediate(itemStatPrefab.transform.parent.GetChild(i).gameObject);
                    }

                    var stats = package.Item.Stats;
                    for (var i = 0; i < stats.Count; i++)
                    {
                        var itemStat = Instantiate(itemStatPrefab, itemStatPrefab.transform.parent);

                        var compareToValue = CompareStatValues(stats[i]);
                        var color = Color.white;

                        if (compareToValue != 0)
                            color = compareToValue < 0 ? Color.red : Color.green;

                        itemStat.text = $"{stats[i].Stat}\t+ {Colored(stats[i].Modifier.Value.ToString(), color)}";

                        itemStat.gameObject.SetActive(true);

                        string Colored(string text, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{text}</color>";
                    }

                    int CompareStatValues(ItemStat stat)
                    {
                        if (compareTo.Item != null)
                            for (var i = 0; i < compareTo.Item.Stats.Count; i++)
                                if (compareTo.Item.Stats[i].Stat == stat.Stat)
                                    return compareTo.Item.Stats[i].Modifier.Value.CompareTo(stat.Modifier.Value);

                        return 0 < stat.Modifier.Value ? 1 : -1;
                    }
                }

                /*  itemValue / sellValue
                 *  durability?
                 *  flavor text?
                 */
            }
        }

        public void SetPackage(Package package, Vector2Int storedPosition)
        {
            RefreshDragDisplay(package);
            StoredPosition = storedPosition;
        }
    }
}
