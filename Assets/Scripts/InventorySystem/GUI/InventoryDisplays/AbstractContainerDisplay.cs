using Submodules.Utility.Services;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Services;
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
        /// Binds itself to the container <see cref="role"/> names, asked of the inventory service,
        /// rather than waiting to be pushed one by a hard scene reference - the seam that lets a
        /// display spawned at runtime (the Vendor's slot grids) bind itself with no Inspector
        /// wiring. Unconditional rather than guarded on <c>Container == null</c>:
        /// with domain/scene reload disabled a display survives Play Mode Stop with a now-stale
        /// <see cref="Container"/> reference, so every enable re-resolves against whatever the
        /// current Hero and World hold (#85's OnEnable-not-Awake lesson). Left unbound while no
        /// service is armed - an enable in Edit Mode.
        ///
        /// A hero load replaces every container (#114), so the registration runs again on
        /// <see cref="ISession.HeroLoaded"/>: <see cref="SetupDisplay"/> lets go of the old container's
        /// content event and binds the new one's.
        /// </summary>
        private void OnEnable()
        {
            _ = Session.TrySubscribeHeroLoaded(Register);

            Register();
        }

        private void OnDisable() => Session.UnsubscribeHeroLoaded(Register);

        /// <summary>False for a display whose container is not a player container, so it is handed one through <see cref="SetupDisplay"/>.</summary>
        protected virtual bool BindsByRole => true;

        private void Register()
        {
            if (!BindsByRole || !ServiceLocator.IsArmed)
                return;

            var container = InventoryService.Instance.ContainerFor(role);
            if (container != null)
                SetupDisplay(container);
        }

#if UNITY_EDITOR
        /// <summary>
        /// The "wiring took" check for <see cref="role"/> - without this, an unwired field would deserialize to
        /// <see cref="ContainerRole.Unassigned"/> and get silently ignored by every consumer
        /// (<see cref="Register"/> just returns without binding), so a
        /// misconfigured display never draws and nothing says why.
        /// </summary>
        private void OnValidate()
        {
            if (BindsByRole && role == ContainerRole.Unassigned && !UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this))
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
        }
    }
}
