using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Displays;
using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;

public abstract class AbstractContainerDisplay : MonoBehaviour
{
    protected AbstractDimensionalContainer Container;

    [SerializeField] protected List<AbstractSlotDisplay> containerSlotDisplays = new();

    public void SetupDisplay(AbstractDimensionalContainer container)
    {
        SetContainer(container);

        SetupSlotDisplays(container);

        Refresh(Container?.storedPackages);
    }

    protected abstract void SetupSlotDisplays(AbstractDimensionalContainer container);

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
        int current = 0;
        for (int x = 0; x < Container?.Dimensions.x; x++)
            for (int y = 0; y < Container?.Dimensions.y; y++)
            {
                storedPackages.TryGetValue(new(x, y), out Package package);

                containerSlotDisplays[current].RefreshSlotDisplay(package);

                // if current == dragDisplayOrigin set it's alpha down, else set it to 1

                current++;
            }
    }
}
