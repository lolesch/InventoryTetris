using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    // TODO: inherit AbstractDisplay or rename this pattern
    internal abstract class AbstractContainerDisplay : MonoBehaviour//AbstractPanel
    {
        protected AbstractDimensionalContainer Container;

        [SerializeField] protected List<AbstractSlotDisplay> containerSlotDisplays = new();
        //[SerializeField] protected Image Icon;

        public void SetupDisplay(AbstractDimensionalContainer container)
        {
            SetContainer(container);

            SetupSlotDisplays();

            Refresh(Container?.StoredPackages);
        }

        protected abstract void SetupSlotDisplays();

        /// Maps a container position to its slot display, using the same flat
        /// x-major indexing Refresh walks the grid with.
        public bool TryGetSlotDisplayAt(Vector2Int position, out AbstractSlotDisplay slotDisplay)
        {
            slotDisplay = null;

            if (Container == null)
                return false;

            var index = (position.x * Container.Dimensions.y) + position.y;

            if (0 > index || containerSlotDisplays.Count <= index)
                return false;

            slotDisplay = containerSlotDisplays[index];

            return slotDisplay != null;
        }

        private void SetContainer(AbstractDimensionalContainer container)
        {
            if (container != Container)
            {
                if (null != Container)
                    Container.OnContentChanged -= Refresh;

                Container = container;

                if (null != Container)
                    Container.OnContentChanged += Refresh;
            }
        }

        protected virtual void Refresh(Dictionary<Vector2Int, Package> storedPackages)
        {
            var current = 0;
            for (var x = 0; x < Container?.Dimensions.x; x++)
                for (var y = 0; y < Container?.Dimensions.y; y++)
                {
                    _ = storedPackages.TryGetValue(new(x, y), out var package);

                    containerSlotDisplays[current].RefreshSlotDisplay(package);

                    // if current == dragDisplayOrigin set it's alpha down, else set it to 1

                    current++;
                }

            //Icon.color = InventoryProvider.Instance.ContainerToAddTo == Container
            //    ? new Color(1, .84f, 0, 1)
            //    : Color.white;
        }
    }
}
