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
            if (!gridLayout) 
                return;
            
            gridLayout.startAxis = GridLayoutGroup.Axis.Vertical;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = Container.Dimensions.x;

            return;

            void InstantiateNewSlots(AbstractSlotDisplay slot)
            {
                var existing = DestroyInvalidSlotDisplays();

                var current = 0;
                containerSlotDisplays.Clear();

                for (var x = 0; x < Container?.Dimensions.x; x++)
                    for (var y = 0; y < Container?.Dimensions.y; y++, current++)
                    {
                        containerSlotDisplays.Add(current < existing.Count
                            ? existing[current]
                            : Instantiate(slot, transform));

                        containerSlotDisplays[current].SetupSlot(this, Container, new(x, y));
                    }

                return;

                List<AbstractSlotDisplay> DestroyInvalidSlotDisplays()
                {
                    var previous = GetComponentsInChildren<AbstractSlotDisplay>().ToList();
                    for (var i = previous.Count; i-- > Container?.Capacity;) // for (var i = previous.Count - 1; Container?.Capacity <= i; i--)
                    {
#if UNITY_EDITOR
                        DestroyImmediate(previous[i].gameObject);
#else
                        Destroy(previous[i].gameObject);
#endif
                        previous.RemoveAt(i);
                    }
                    return previous;
                }
            }
        }
    }
}
