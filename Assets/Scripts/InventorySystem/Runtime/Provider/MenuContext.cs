using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.GUI.Components.Toggles;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// Publishes which secondary panel is open - the quick-move context (issue #30). A
    /// thin adapter over the menu's <see cref="RadioGroup"/>: on every
    /// <see cref="RadioGroup.OnGroupChanged"/> - a sibling taking over, or the active
    /// toggle switching itself off (which clears the group) - it re-derives the kind from
    /// the active toggle's authored <see cref="MenuPanelToggle.MenuContextKind"/>, never
    /// which toggle is which. The container a kind quick-moves to is published alongside,
    /// so the slot displays can hand the resolver its inputs.
    /// </summary>
    public sealed class MenuContext : AbstractProvider<MenuContext>
    {
        [SerializeField] private RadioGroup menuToggles;

        public MenuContextKind CurrentKind { get; private set; } = MenuContextKind.None;
        public AbstractDimensionalContainer CurrentContainer { get; private set; }

        private void OnEnable()
        {
            if (menuToggles == null)
                return;

            menuToggles.OnGroupChanged += Refresh;

            Refresh();
        }

        private void OnDisable()
        {
            if (menuToggles == null)
                return;

            menuToggles.OnGroupChanged -= Refresh;
        }

        private void Refresh()
        {
            var kind = KindOf(menuToggles.ActivatedToggle);

            CurrentKind = kind;
            CurrentContainer = ContainerOf(kind);
        }

        private static MenuContextKind KindOf(AbstractToggle toggle) =>
            toggle is MenuPanelToggle menuToggle ? menuToggle.MenuContextKind : MenuContextKind.None;

        private static AbstractDimensionalContainer ContainerOf(MenuContextKind kind) => kind switch
        {
            MenuContextKind.Stash => InventoryProvider.Instance.Stash,
            MenuContextKind.Store => InventoryProvider.Instance.Store,
            _ => null,
        };
    }
}