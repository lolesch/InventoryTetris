using TMPro;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.InventoryDisplays
{
    /// <summary>
    /// The GUI shell of the Sell Basket (issue #66 - the scene half of #32, split out because
    /// <c>feature/trade-flow</c> makes no scene edits). Sits below the Supply grid in the Vendor
    /// Side Panel and owns the staged-sale flow:
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
    /// <para>This file is intentionally C#-only: the Unity source tree on <c>feature/trade-flow</c>
    /// carries no scene edits (issues #56 / #57 own that region), so every  wiring contract the
    /// prefab needs is spelled out on the serialized fields.</para>
    ///
    /// <para>Drag-hold is a <see cref="Package"/> under <see cref="DragProvider.DraggingPackage"/>;
    /// it is staged straight from the cursor because the drag origin slot already removed it on
    /// pick-up. The new overlays the dragged Package's footprint in the basket (one
    /// <see cref="ItemTransaction"/>), so the pre-history <see cref="SellBasket.Stage"/>
    /// (origin already removed) and its path (new overlay) agree.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(20)]
    internal sealed class SellBasketPanel : MonoBehaviour
    {
        /// <summary>The inventory grid the player stages a sale into. Unwired until the prefab
        /// exists: <see cref="InventoryProvider.BasketDisplay"/> holds the same grid we bind,
        /// and <see cref="InventoryProvider.Basket"/> holds the basket logic.</summary>
        [SerializeField, Tooltip("The basket grid - one BasketSlotDisplay per cell. Wire to the prefab's InventoryContainerDisplay; absent in the C#-only draft.")]
        private InventoryContainerDisplay basketDisplay;

        /// <summary>The caption for the staged total. Wired when the prefab exists.</summary>
        [SerializeField, Tooltip("The running total the vendor would pay. Wire to the panel's TMP label; absent in the C#-only draft.")]
        private TextMeshProUGUI totalLabel;

        /// <summary>Bank the staged sale (one consolidated payout) and clear the basket.</summary>
        [SerializeField, Tooltip("Wire to the panel's Confirm Button; absent in the C#-only draft.")]
        private Button confirmButton;

        /// <summary>Return every staged Package to its origin, wallet untouched.</summary>
        [SerializeField, Tooltip("Wire to the panel's Cancel Button; absent in the C#-only draft.")]
        private Button cancelButton;

        /// <summary>
        /// The Supply region's <see cref="CanvasGroup"/> (the blocker). While the basket holds
        /// anything the group is disabled and dimmed - the modal sell block (ADR-0012). Its
        /// <see cref="CanvasGroup"/> is deliberately NOT referenced by the prefab: this draft
        /// exists before the Supply/panel scene, so the blocker is found by name at runtime and
        /// the <c>OnValidate</c> warning AND the null-guard are exactly the "wiring took" check
        /// the issue's blocker criterion describes.
        /// </summary>
        [SerializeField, Tooltip("The Supply region's CanvasGroup to block while the basket is non-empty. Wired once the prefab exists; found by name in the C#-only draft.")]
        private CanvasGroup supplyBlocker;

        private SellBasket.Basket basket;
        private Wallet wallet;

        private void Awake()
        {
            if (!TryResolveDependencies())
                return;

            basketDisplay.SetupDisplay(basket.Container);
        }

        private void OnEnable()
        {
            var provider = InventoryProvider.Instance;

            provider.OnSidePanelChanged -= OnSidePanelChanged;
            provider.OnSidePanelChanged += OnSidePanelChanged;

            if (basket.Container != null)
                basket.Container.OnContentChanged -= OnBasketContentChanged;
            if (basket.Container != null)
                basket.Container.OnContentChanged += OnBasketContentChanged;

            RefreshUi();
        }

        private void OnDisable()
        {
            if (InventoryProvider.Instance != null)
                InventoryProvider.Instance.OnSidePanelChanged -= OnSidePanelChanged;

            if (basket?.Container != null)
                basket.Container.OnContentChanged -= OnBasketContentChanged;
        }

        /// <summary>
        /// Wire the reserved fields. The prefab resolves <b>basketDisplay</b> from
        /// <see cref="InventoryProvider.BasketDisplay"/> and <b>supplyBlocker</b> by searching
        /// the Vendor Side Panel for "Supply" - both with zero serialized references in the
        /// C#-only draft. Confirm/Cancel are serialized exclusively.
        /// </summary>
        private bool TryResolveDependencies()
        {
            basket = InventoryProvider.Instance.Basket;
            wallet = InventoryProvider.Instance.Wallet;

            if (basket == null || wallet == null)
                return false;

            if (basketDisplay == null)
                basketDisplay = InventoryProvider.Instance.BasketDisplay;

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
        /// The Supply region inside the Vendor Side Panel - the blocked territory while the
        /// basket holds anything. This is the modal-sell contract: it must NOT be this panel or
        /// the Supply grid, so a named search is the honest fallback while the panel scene does
        /// not yet exist.
        /// </summary>
        private CanvasGroup FindSupplyBlocker()
        {
            var panel = transform.root;

            foreach (var group in panel.GetComponentsInChildren<CanvasGroup>(true))
                if (group.name.ToLowerInvariant().Contains("supply"))
                    return group;

            return null;
        }

        private void OnBasketContentChanged() => RefreshUi();

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
                return;

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
        /// Vendor panel closing while the basket holds anything
        /// (<see cref="OnSidePanelChanged"/>) - a staged sale is never silently stranded.
        ///
        /// <para>A Package that fits neither its origin nor the backpack is handed back on the
        /// cursor when a drag is live (the same fallback a cancelled drag uses); with no drag it
        /// is surfaced rather than lost - it never vanishes silently, because the seam's
        /// "never destroyed" contract is the caller's to honour.</para>
        /// </summary>
        public void Cancel()
        {
            if (basket == null)
                return;

            var backpack = InventoryProvider.Instance.Inventory;
            var leftover = SellBasket.Cancel(basket, backpack);

            if (leftover.IsValid)
            {
                if (DragProvider.Instance != null && DragProvider.Instance.IsDragging)
                    DragProvider.Instance.ReplacePackage(leftover, default); // keep it on the cursor
                else
                    Debug.LogWarning($"[SellBasket] A staged Package had nowhere to go on Cancel and was left undropped; wallet untouched.", this);
            }
        }

        /// <summary>
        /// The Vendor side panel closed and the basket still holds staged Packages - that is a
        /// Cancel, exactly as the issue's "closing the Store with a non-empty basket" criterion
        /// says. Subscribed in <see cref="OnEnable"/> (detach-before-attach, cf. da14ce2) so a
        /// re-open cannot stack a second subscription.
        /// </summary>
        private void OnSidePanelChanged(SidePanelContext context)
        {
            if (context == SidePanelContext.Vendor)
                return; // opened - keep whatever is staged

            if (basket != null && basket.Container != null && basket.Container.StoredPackages.Count > 0)
                Cancel();
        }
    }
}