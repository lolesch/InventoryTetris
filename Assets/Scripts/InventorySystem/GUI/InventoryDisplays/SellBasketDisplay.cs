using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.GUI.Components.Panels;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;
using UnityEngine.UI;

[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    /// <summary>
    /// The GUI shell of the Sell Basket (issue #66 - the scene half of #32). Placed in the
    /// Vendor Side Panel below the Supply grid (<c>SellBasketDisplay.prefab</c>) and owns the
    /// staged-sale flow:
    ///
    /// <list type="number">
    /// <item><b>Stage by drop</b> - a Package dragged onto the basket grid lands in a cell and
    /// its origin is remembered, so a Cancel can return it there.</item>
    /// <item><b>Preview</b> - the running total <see cref="SellBasket.PreviewValue"/> the vendor
    /// would pay, a pure sum, refreshed whenever the basket content changes.</item>
    /// <item><b>Confirm</b> - runs one <see cref="SellBasket.Confirm"/> over the basket and the
    /// wallet: one consolidated payout, basket cleared, value conserved.</item>
    /// <item><b>Cancel</b> - returns every staged Package through the return-to-origin primitive,
    /// wallet untouched.</item>
    /// <item><b>Panel-close</b> - the Vendor side panel closing while the basket still holds
    /// anything cancels the sale the same way (a staged sale is never silently stranded).</item>
    /// <item><b>Modal sell</b> - while the basket holds anything the whole Supply region stops
    /// responding and dims; it re-enables the moment the basket empties. This is load-bearing,
    /// not polish: it keeps the free backpack room each basket Package came from, so a
    /// backpack-origin Cancel always fits.</item>
    /// </list>
    ///
    /// <para>Drag-hold is a <see cref="Package"/> under <see cref="DragProvider.DraggingPackage"/>;
    /// it is staged straight from the cursor because the drag origin slot already removed it on
    /// pick-up. The new overlays the dragged Package's footprint in the basket (one
    /// <see cref="ItemTransaction"/>), so the pre-history <see cref="SellBasket.Stage"/>
    /// (origin already removed) and its path (new overlay) agree.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(20)]
    internal sealed class SellBasketDisplay : MonoBehaviour
    {
        /// <summary>The inventory grid the player stages a sale into - binds itself to
        /// <see cref="InventoryProvider.Basket"/>'s container via <see cref="ContainerRole.Basket"/>
        /// (<see cref="AbstractContainerDisplay.OnEnable"/>), and <see cref="InventoryProvider.Basket"/>
        /// holds the basket logic behind it.</summary>
        [SerializeField, Tooltip("The basket grid - one BasketSlotDisplay per cell. Wired to the prefab's InventoryContainerDisplay.")]
        private InventoryContainerDisplay basketDisplay;

        /// <summary>The caption for the staged total.</summary>
        [SerializeField, Tooltip("The running total the vendor would pay. Wired to the panel's TMP label.")]
        private TextMeshProUGUI totalLabel;

        /// <summary>Bank the staged sale (one consolidated payout) and clear the basket.</summary>
        [SerializeField, Tooltip("Wired to the panel's Confirm Button.")]
        private Button confirmButton;

        /// <summary>Return every staged Package to its origin, wallet untouched.</summary>
        [SerializeField, Tooltip("Wired to the panel's Cancel Button.")]
        private Button cancelButton;

        /// <summary>
        /// The Supply region's <see cref="CanvasGroup"/> (the blocker) - the Shop grid's own
        /// <see cref="CanvasGroup"/> in the Vendor Side Panel. While the basket holds anything
        /// the group is disabled and dimmed - the modal sell block (ADR-0012). Wired directly
        /// on the scene instance rather than baked into the shared prefab, since the Supply
        /// region is scene-specific; <see cref="FindSupplyBlocker"/> is the fallback for a
        /// misconfigured instance, and <see cref="OnValidate"/> plus the first-stage error in
        /// <see cref="SetSupplyBlocked"/> are the "wiring took" check the issue's blocker
        /// criterion describes.
        /// </summary>
        [SerializeField, Tooltip("The Supply region's CanvasGroup to block while the basket is non-empty. Wired on the scene instance.")]
        private CanvasGroup supplyBlocker;

        private SellBasket.Basket basket;
        private Wallet wallet;

        /// <summary>The Side Panel this display lives in. There is one Sell Basket and one
        /// display per Town Stop that sells (the Vendor's and the Healer's, issue #121), so which
        /// Town Stop a display belongs to - and so what blocks, and when it cancels - is read off
        /// the panel it sits in rather than assumed to be the Vendor.</summary>
        private SidePanel ownerPanel;

        /// <summary>The Inventory Context this display's panel is authored with; falls back to
        /// the Vendor for a display placed outside any Side Panel (the original single-basket
        /// layout).</summary>
        private InventoryContext OwnContext => ownerPanel != null ? ownerPanel.Context : InventoryContext.Vendor;

        private void Awake()
        {
            // Reading InventoryProvider.Instance outside Play mode can create one (issue #46) -
            // Awake also runs at edit time (a domain reload, entering/exiting prefab isolation).
            if (!Application.isPlaying)
                return;

            if (!TryResolveDependencies())
                return;
        }

        private void OnEnable()
        {
            _ = InventoryProvider.TrySubscribeContextChanged(OnContextChanged, out _);

            if (basket.Container != null)
            {
                basket.Container.OnContentChanged -= OnBasketContentChanged;
                basket.Container.OnContentChanged += OnBasketContentChanged;
            }

            RefreshUi();
        }

        private void OnDisable()
        {
            InventoryProvider.UnsubscribeContextChanged(OnContextChanged);

            if (basket?.Container != null)
                basket.Container.OnContentChanged -= OnBasketContentChanged;
        }

        /// <summary>
        /// Wire the reserved fields. <b>basketDisplay</b> and <b>supplyBlocker</b> are wired
        /// directly on the scene instance; the by-name <see cref="FindSupplyBlocker"/> search
        /// and the child-search fallback below are fallbacks for a misconfigured instance, not
        /// the primary path. Confirm/Cancel are serialized exclusively.
        /// </summary>
        private bool TryResolveDependencies()
        {
            basket = InventoryProvider.Instance.Basket;
            wallet = InventoryProvider.Instance.Wallet;
            ownerPanel = GetComponentInParent<SidePanel>(true);

            if (basket == null || wallet == null)
                return false;

            if (basketDisplay == null)
                basketDisplay = GetComponentInChildren<InventoryContainerDisplay>();

            if (basketDisplay == null)
                return false; // no grid - nothing to draw

            if (supplyBlocker == null)
                supplyBlocker = FindSupplyBlocker();

            if (totalLabel != null)
                totalLabel.text = SellBasket.PreviewValue(basket).ToString();

            if (confirmButton != null)
                confirmButton.onClick.AddListener(Confirm);
            if (cancelButton != null)
                cancelButton.onClick.AddListener(Cancel);

            return true;
        }

        /// <summary>
        /// The Supply region inside this display's own Side Panel - the blocked territory while
        /// the basket holds anything. <see cref="supplyBlocker"/> is wired directly on the scene
        /// instance; this by-name search only covers a misconfigured instance where that
        /// reference was left empty. Scoped to the owning panel, not the whole Canvas: with a
        /// second selling Town Stop (the Healer, issue #121) a Canvas-wide search found the
        /// Vendor's Supply first and so blocked the wrong shelf.
        /// </summary>
        private CanvasGroup FindSupplyBlocker()
        {
            var panel = ownerPanel != null ? ownerPanel.transform : transform.root;

            foreach (var group in panel.GetComponentsInChildren<CanvasGroup>(true))
                if (group.transform != panel && (group.name.ToLowerInvariant().Contains("supply") || group.name.ToLowerInvariant().Contains("shop")))
                    return group;

            return null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// The Editor half of the "wiring took" check the issue's blocker criterion describes
        /// - a missing <see cref="supplyBlocker"/> would otherwise sit silent until someone
        /// notices the Supply grid staying interactable during a staged sale. Skipped for the
        /// shared <c>SellBasketDisplay.prefab</c> asset itself, where <see cref="supplyBlocker"/>
        /// is null by design (the Supply CanvasGroup is scene-specific, wired per instance) - a
        /// green run has to mean the scene instance is wired, not just that nobody has opened
        /// the prefab lately.
        /// </summary>
        private void OnValidate()
        {
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this))
                return;

            if (supplyBlocker == null)
                Debug.LogWarning("[SellBasketDisplay] supplyBlocker is not wired - the Supply region will not be blocked while a sale is staged.", this);
            else
            {
                var panel = GetComponentInParent<SidePanel>(true);

                if (panel != null && !supplyBlocker.transform.IsChildOf(panel.transform))
                    Debug.LogWarning("[SellBasketDisplay] supplyBlocker lives in a different Side Panel - this display would block another Town Stop's Supply and leave its own open.", this);
            }
        }
#endif

        private void OnBasketContentChanged(Dictionary<Vector2Int, Package> _) => RefreshUi();

        /// <summary>
        /// The sum shown under the basket - <see cref="SellBasket.PreviewValue"/>, the same
        /// value a Confirm banks, so what the player commits equals what they previewed.
        /// </summary>
        private void RefreshUi()
        {
            if (totalLabel != null)
                totalLabel.text = SellBasket.PreviewValue(basket).ToString();

            var hasItems = basket.Container != null && basket.Container.StoredPackages.Count > 0;

            if (confirmButton != null)
                confirmButton.interactable = hasItems;
            if (cancelButton != null)
                cancelButton.interactable = hasItems;

            SetSupplyBlocked(hasItems);
        }

        /// <summary>
        /// The modal sell block (ADR-0012): while the basket holds anything the whole Supply
        /// region is non-interactable and dims. It re-enables the instant the basket empties.
        /// Empties also arm Confirm/Cancel, so the sale flow stays honest at every state.
        /// </summary>
        private void SetSupplyBlocked(bool blocked)
        {
            if (supplyBlocker == null)
            {
                if (blocked)
                    Debug.LogError("[SellBasketDisplay] A sale was staged with no supplyBlocker wired - the Supply region will not be blocked.", this);

                return;
            }

            supplyBlocker.blocksRaycasts = !blocked;
            supplyBlocker.interactable = !blocked;
            supplyBlocker.alpha = blocked ? 0.5f : 1f;
        }

        /// <summary>
        /// Bank the staged sale: one consolidated payout equal to the preview, then clear the
        /// basket (issue #32). Wallet untouched otherwise; value conserved by construction.
        /// Reached from the Confirm button, so no drag is in flight - nothing to end.
        /// </summary>
        public void Confirm()
        {
            if (basket == null || wallet == null)
                return;

            _ = SellBasket.Confirm(basket, wallet);
        }

        /// <summary>
        /// Cancel the staged sale: return every Package through the return-to-origin primitive
        /// (<see cref="SellBasket.Cancel"/>), wallet untouched. Called by Cancel, and by the
        /// Vendor context going away while the basket holds anything
        /// (<see cref="OnContextChanged"/>) - a staged sale is never silently stranded.
        ///
        /// <para>A Package that fits neither its origin nor the backpack is handed back on the
        /// cursor when a drag is live (the same fallback a cancelled drag uses); with no drag it
        /// is kept staged in the basket instead - it never vanishes silently, because the seam's
        /// "never destroyed" contract is this caller's to honour, not just <see cref="SellBasket"/>'s.</para>
        /// </summary>
        public void Cancel()
        {
            if (basket == null)
                return;

            var backpack = InventoryProvider.Instance.Inventory;
            var leftover = SellBasket.Cancel(basket, backpack);

            if (!leftover.IsValid)
                return;

            if (DragProvider.Instance != null && DragProvider.Instance.IsDragging)
            {
                DragProvider.Instance.ReplacePackage(leftover, default); // keep it on the cursor
                return;
            }

            /// No drag is in flight to hand this to - the Vendor closing via a context change
            /// (Send, Recall, Death, Go Venture) drives this same Cancel with nothing on the
            /// cursor. SellBasket.Cancel already removed it from the basket once it could not
            /// return to its origin or the backpack; without this, the Package would simply
            /// vanish, breaking SellBasket's own "never destroyed" contract. Keep it staged so
            /// it stays visible and recoverable instead.
            var restaged = leftover;

            if (!basket.Container.TryAddToContainer(ref restaged))
                Debug.LogError($"[SellBasketDisplay] A staged Package could not be returned on Cancel, and the basket had no room to keep it either - it was lost.", this);
        }

        /// <summary>
        /// The Inventory Context left this display's own Town Stop and the basket still holds
        /// staged Packages - that is a Cancel, exactly as the issue's "closing the Store with a
        /// non-empty basket" criterion says. Subscribed in <see cref="OnEnable"/>
        /// (detach-before-attach, cf. da14ce2) so a re-open cannot stack a second subscription.
        ///
        /// <para>The guard is on "is not my Town Stop" rather than on the panel's own visibility,
        /// so every route that leaves it counts - closing the panel, closing the Hero Panel with
        /// it, Send, Recall, Death and Go Venture all land here as some other context. A handover
        /// to another Town Stop - the Vendor to the Healer included, though both have a basket -
        /// is a Cancel too: activating one Town Stop cancels whatever the others had in flight
        /// (#58's ruling). There is one basket and one display per selling stop, so the display
        /// of the stop being left is the one that cancels.</para>
        /// </summary>
        private void OnContextChanged(InventoryContext context)
        {
            if (context == OwnContext)
                return; // opened - keep whatever is staged

            if (basket != null && basket.Container != null && basket.Container.StoredPackages.Count > 0)
                Cancel();
        }
    }
}