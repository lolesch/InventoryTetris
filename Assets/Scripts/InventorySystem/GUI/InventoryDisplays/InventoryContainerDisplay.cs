using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    [RequireComponent(typeof(GridLayoutGroup))]

    [System.Serializable]
    internal sealed class InventoryContainerDisplay : AbstractContainerDisplay
    {
        [SerializeField] private AbstractSlotDisplay slotDisplayPrefab;

        //private void Awake()
        //{
        //    var gridLayout = GetComponent<GridLayoutGroup>();
        //    if (gridLayout)
        //    {
        //        gridLayout.startAxis = GridLayoutGroup.Axis.Vertical;
        //        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        //        gridLayout.constraintCount = Container.Dimensions.x;
        //    }
        //}

        protected override void SetupSlotDisplays()
        {
            // TODO: make it a prefab pool instead
            if (slotDisplayPrefab)
                InstantiateNewSlots(slotDisplayPrefab);

            var gridLayout = GetComponent<GridLayoutGroup>();
            if (gridLayout)
            {
                gridLayout.startAxis = GridLayoutGroup.Axis.Vertical;
                gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                gridLayout.constraintCount = Container.Dimensions.x;
            }

            void InstantiateNewSlots(AbstractSlotDisplay slot)
            {
                var slotDisplays = DestroyInvalidSlotDisplays();

                var current = 0;
                containerSlotDisplays.Clear();

                for (var x = 0; x < Container?.Dimensions.x; x++)
                    for (var y = 0; y < Container?.Dimensions.y; y++, current++)
                    {
                        if (current < slotDisplays.Count)
                            containerSlotDisplays.Add(slotDisplays[current]);
                        else
                            containerSlotDisplays.Add(Instantiate(slot, transform));

                        containerSlotDisplays[current].SetupSlot(Container, new(x, y));
                    }

                List<InventorySlotDisplay> DestroyInvalidSlotDisplays()
                {
                    var slotDisplays = GetComponentsInChildren<InventorySlotDisplay>().ToList();

                    for (var i = slotDisplays.Count - 1; Container?.Capacity <= i; i--)
                    {
#if UNITY_EDITOR
                        DestroyImmediate(slotDisplays[i].gameObject);
#else
                        Destroy(slotDisplays[i].gameObject);
#endif
                        slotDisplays.RemoveAt(i);
                    }
                    return slotDisplays;
                }
            }
        }
    }
}
