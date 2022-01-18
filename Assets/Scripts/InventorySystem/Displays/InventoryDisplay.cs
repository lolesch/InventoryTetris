using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;
using UnityEngine.UI;

[assembly: InternalsVisibleTo("Tests")]

namespace ToolSmiths.InventorySystem.Displays
{
    [System.Serializable]
    public class InventoryDisplay : MonoBehaviour
    {
        protected internal AbstractContainer Container;

        protected internal Dictionary<Vector2Int, InventorySlotDisplay> slotDisplays = new Dictionary<Vector2Int, InventorySlotDisplay>();

        [SerializeField] protected internal InventorySlotDisplay slotDisplayPrefab;

        public void SetupInventory(AbstractContainer container)
        {
            SetInventory(container);

            SetupLayoutGroup();

            void SetupLayoutGroup()
            {
                GridLayoutGroup gridLayout = GetComponent<GridLayoutGroup>();
                if (gridLayout)
                {
                    gridLayout.startAxis = GridLayoutGroup.Axis.Vertical;
                    gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                    gridLayout.constraintCount = Container.Dimensions.x;
                }
            }
        }

        protected internal void SetInventory(AbstractContainer container)
        {
            if (container != Container)
            {
                if (null != Container)
                    Container.OnContentChanged -= Refresh;

                Container = container;

                if (null != Container)
                    Container.OnContentChanged += Refresh;
            }

            if (slotDisplayPrefab)
                InstantiateNewSlots(slotDisplayPrefab);

            Refresh(Container?.StoredPackages);

            // TODO: pool slots
            void InstantiateNewSlots(InventorySlotDisplay slot)
            {
                var slotDisplays = DestroyInvalidSlotDisplays();

                int current = 0;
                this.slotDisplays.Clear();
                for (int x = 0; x < Container?.Dimensions.x; x++)
                    for (int y = 0; y < Container?.Dimensions.y; y++, current++)
                    {
                        if (current < slotDisplays.Count)
                            this.slotDisplays.Add(new(x, y), slotDisplays[current]);
                        else
                            this.slotDisplays.Add(new(x, y), Instantiate(slot, transform));

                        this.slotDisplays[new(x, y)].Position = new(x, y);
                        this.slotDisplays[new(x, y)].name = $" {x} | {y}";
                        this.slotDisplays[new(x, y)].Container = Container;
                    }

                List<InventorySlotDisplay> DestroyInvalidSlotDisplays()
                {
                    List<InventorySlotDisplay> slotDisplays = GetComponentsInChildren<InventorySlotDisplay>().ToList();

                    for (int i = slotDisplays.Count - 1; Container?.Capacity <= i; i--)
                    {
#if UNITY_EDITOR
                        DestroyImmediate(slotDisplays[i].gameObject);
#else
                Destroy(children[i].gameObject);
#endif
                        slotDisplays.RemoveAt(i);
                    }
                    return slotDisplays;
                }
            }
        }

        private void Refresh(Dictionary<Vector2Int, Package> storedPackages)
        {
            for (int x = 0; x < Container?.Dimensions.x; x++)
                for (int y = 0; y < Container?.Dimensions.y; y++)
                {
                    storedPackages.TryGetValue(new(x, y), out Package item);
                    slotDisplays[new(x, y)].RefreshItemDisplay(item);
                }
        }
    }
}
