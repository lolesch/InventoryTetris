using System;
using System.Collections;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// One pooled entry of the floor views (issues #63, #221), bound to one Drop at a time: its
    /// icon, its name in the rarity colour (a list row; a grid cell has no label), a hover preview
    /// through <see cref="PreviewProvider"/> and a click that reports the Drop back. Not an
    /// <c>AbstractSlotDisplay</c> - a ground slot has no container, no cell and nothing to drag - so
    /// it carries only the hover pattern, not the container machinery. <see cref="GroundItemsPanel"/>
    /// and <see cref="GroundGridPanel"/> are the only intended callers of <see cref="Bind"/> and
    /// <see cref="Unbind"/>, and own what a click does. A pooled entry is fully reset on
    /// <see cref="Unbind"/>: the next Drop finds nothing of the last one.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class GroundItemSlotDisplay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField, Tooltip("Tinted to the item's rarity. Optional.")] private Image rarityBorder;

        private Action<ItemInstance> _onClick;
        private bool _previewShown;
        private Coroutine _pendingPreview;
        private CanvasGroup _group;

        private CanvasGroup Group => _group ? _group : _group = GetComponent<CanvasGroup>();

        /// <summary>The Drop this entry shows, or <c>null</c> while it sits in the pool.</summary>
        public ItemInstance Item { get; private set; }

        /// <summary>How many of <see cref="Item"/> the entry's Package holds.</summary>
        public uint Amount { get; private set; }

        /// <summary>
        /// Show a Package of <paramref name="amount"/> of <paramref name="item"/>, drawn from
        /// <paramref name="view"/>, and call <paramref name="onClick"/> with the item when clicked. An entry
        /// re-bound to a different Drop drops its hover first: the preview it was showing described the old one.
        /// </summary>
        public void Bind(ItemInstance item, uint amount, ItemView view, Action<ItemInstance> onClick)
        {
            _onClick = onClick;

            if (ReferenceEquals(Item, item) && Amount == amount)
                return;

            HidePreview();
            Item = item;
            Amount = amount;

            if (icon != null)
            {
                icon.sprite = view.Icon;
                icon.color = Color.white;
            }

            if (nameLabel != null)
            {
                nameLabel.text = 1u < amount ? $"{view.DisplayName} x{amount}" : view.DisplayName;
                nameLabel.color = view.RarityColor;
            }

            if (rarityBorder != null)
                rarityBorder.color = view.RarityColor;
        }

        /// <summary>How opaque the entry is drawn: the grid fades a Drop by its age rank (<see cref="GroundFade"/>).</summary>
        public void Fade(float alpha) => Group.alpha = alpha;

        /// <summary>Let go of the Drop and everything drawn for it. Safe when nothing is bound.</summary>
        public void Unbind()
        {
            HidePreview();
            Item = null;
            Amount = 0u;
            _onClick = null;
            Fade(1f);

            if (icon != null)
                icon.sprite = null;

            if (nameLabel != null)
                nameLabel.text = string.Empty;

            if (rarityBorder != null)
                rarityBorder.color = Color.white;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // An enter with no exit in between (a row re-activated under a resting cursor) must not
            // leave the earlier wait running: HidePreview only knows the latest handle.
            HidePreview();

            if (Item != null)
                _pendingPreview = StartCoroutine(ShowPreviewAfterDelay(Item, Amount));
        }

        public void OnPointerExit(PointerEventData eventData) => HidePreview();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Item == null)
                return;

            // The click can remove this very row, so the preview goes first.
            HidePreview();
            _onClick?.Invoke(Item);
        }

        // A deactivated row gets no pointer-exit, and its preview would stay up over nothing.
        private void OnDisable() => HidePreview();

        private IEnumerator ShowPreviewAfterDelay(ItemInstance item, uint amount)
        {
            yield return new WaitForSeconds(HoverPreview.Delay);

            _pendingPreview = null;

            // No slot: the compare tooltip's equipment-slot and vendor-price branches are
            // keyed on the slot type, and a ground Drop is neither.
            PreviewProvider.Instance.RefreshPreviewDisplay(new Package(null, item, amount), null);
            _previewShown = true;
        }

        /// <summary>Cancels a preview still waiting out its delay and takes down one already up.</summary>
        private void HidePreview()
        {
            if (_pendingPreview != null)
                StopCoroutine(_pendingPreview);

            _pendingPreview = null;

            if (!_previewShown)
                return;

            _previewShown = false;

            // The provider is gone while Play Mode tears the scene down.
            if (PreviewProvider.Instance != null)
                PreviewProvider.Instance.RefreshPreviewDisplay(new Package(null, null, 0u), null);
        }
    }
}
