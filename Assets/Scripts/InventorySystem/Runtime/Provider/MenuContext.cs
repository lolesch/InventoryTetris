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
    ///
    /// <para>Deliberately <em>not</em> an <see cref="AbstractProvider{T}"/>: this is
    /// scene-scoped UI state that lives on the menu object, next to the
    /// <see cref="RadioGroup"/> it observes. <see cref="AbstractProvider{T}.Instance"/>
    /// promotes its GameObject to a scene root and <c>DontDestroyOnLoad</c>s it - which
    /// here would tear the menu toggles out of the Canvas, where a <c>CanvasRenderer</c>
    /// has no canvas to draw into and they stop rendering. All this needs is a lazy
    /// lookup, not a persisted singleton.</para>
    /// </summary>
    public sealed class MenuContext : MonoBehaviour
    {
        [SerializeField] private RadioGroup menuToggles;

        private static MenuContext instance;

        /// <summary>
        /// The <see cref="MenuContext"/> in the loaded scene. Falls back to a bare logic
        /// object when the scene authored none, so a quick-move with no menu wired still
        /// resolves - to <see cref="MenuContextKind.None"/>, i.e. nothing moves - rather
        /// than throwing. Not carried across a scene load: the next scene's Awake re-points
        /// this at that scene's own context.
        /// </summary>
        public static MenuContext Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<MenuContext>();

                    if (instance == null)
                        instance = new GameObject(nameof(MenuContext)).AddComponent<MenuContext>();
                }

                return instance;
            }
        }

        public MenuContextKind CurrentKind { get; private set; } = MenuContextKind.None;
        public AbstractDimensionalContainer CurrentContainer { get; private set; }

        private void Awake() => instance = this;

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