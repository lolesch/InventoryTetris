using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    // TODO: inherit AbstractDisplay or rename this pattern
    internal abstract class AbstractContainerDisplay : MonoBehaviour//SimplePanel
    {
        protected AbstractDimensionalContainer Container;

        [SerializeField] protected List<AbstractSlotDisplay> containerSlotDisplays = new();
        //[SerializeField] protected Image Icon;

        /// <summary>Which player container this display binds to - see <see cref="ContainerRole"/>.</summary>
        [SerializeField] private ContainerRole role;

        /// <summary>
        /// Self-registers with <see cref="InventoryProvider"/> for <see cref="role"/> rather than
        /// waiting to be pushed a container by a hard scene reference - the seam that lets a
        /// display spawned at runtime (the Vendor's slot grids) bind itself with no Inspector
        /// wiring back on the provider. Unconditional rather than guarded on <c>Container == null</c>:
        /// with domain/scene reload disabled a display survives Play Mode Stop with a now-stale
        /// <see cref="Container"/> reference, so every enable re-resolves against whatever provider
        /// is live (issue #46, #85's OnEnable-not-Awake lesson).
        /// </summary>
        private void OnEnable() => _ = InventoryProvider.TryRegisterDisplay(this, role);

#if UNITY_EDITOR
        /// <summary>
        /// The "wiring took" check for <see cref="role"/>, mirroring
        /// <see cref="ToolSmiths.InventorySystem.GUI.InventoryDisplays.SellBasketDisplay"/>'s
        /// <c>supplyBlocker</c> check - without this, an unwired field would deserialize to
        /// <see cref="ContainerRole.Unassigned"/> and get silently ignored by every consumer
        /// (<see cref="InventoryProvider.TryRegisterDisplay"/> just returns false), so a
        /// misconfigured display never draws and nothing says why.
        /// </summary>
        private void OnValidate()
        {
            if (role == ContainerRole.Unassigned && !UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this))
                Debug.LogWarning($"[{GetType().Name}] role is not wired - this display will never bind to a container.", this);
        }
#endif

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
