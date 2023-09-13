using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    // TODO: inherit AbstractDisplay or rename this pattern
    public abstract class AbstractContainerDisplay : MonoBehaviour//AbstractPanel
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

        private void Refresh(Dictionary<Vector2Int, Package> storedPackages)
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
