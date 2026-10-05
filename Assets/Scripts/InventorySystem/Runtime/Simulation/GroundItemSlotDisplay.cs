using System;
using System.Collections;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Runtime.Provider;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// One pooled row of the Ground Items List (issue #63), bound to one Drop at a time: its
    /// icon, its name in the rarity colour, a hover preview through <see cref="PreviewProvider"/>
    /// and a click that reports the Drop back. Not an <c>AbstractSlotDisplay</c> - a ground slot
    /// has no container, no cell and nothing to drag - so it carries only the hover pattern, not
    /// the container machinery. <see cref="GroundItemsPanel"/> is the only intended caller of
    /// <see cref="Bind"/> and <see cref="Unbind"/>, and owns what a click does.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GroundItemSlotDisplay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        // The delay AbstractSlotDisplay's hover waits before the preview fades in, so passing the
        // cursor down the list does not flash a tooltip per row.
        private const float PreviewDelay = 0.5f;

        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField, Tooltip("Tinted to the item's rarity. Optional.")] private Image rarityBorder;

        private Action<ItemInstance> _onClick;
        private bool _previewShown;

        /// <summary>The Drop this row shows, or <c>null</c> while it sits in the pool.</summary>
        public ItemInstance Item { get; private set; }

        /// <summary>
        /// Show <paramref name="item"/> and call <paramref name="onClick"/> with it when clicked.
        /// A row re-bound to a different Drop drops its hover first: the preview it was showing
        /// described the old one.
        /// </summary>
        public void Bind(ItemInstance item, Action<ItemInstance> onClick)
        {
            _onClick = onClick;

            if (ReferenceEquals(Item, item))
                return;

            HidePreview();
            Item = item;

            var view = ItemService.Instance.View(item);

            if (icon != null)
            {
                icon.sprite = view.Icon;
                icon.color = Color.white;
            }

            if (nameLabel != null)
            {
                nameLabel.text = view.DisplayName;
                nameLabel.color = view.RarityColor;
            }

            if (rarityBorder != null)
                rarityBorder.color = view.RarityColor;
        }

        /// <summary>Let go of the Drop. Safe when nothing is bound.</summary>
        public void Unbind()
        {
            HidePreview();
            Item = null;
            _onClick = null;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Item != null)
                _ = StartCoroutine(ShowPreviewAfterDelay(Item));
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

        private IEnumerator ShowPreviewAfterDelay(ItemInstance item)
        {
            var stamp = Time.time;
            while (Time.time - stamp <= PreviewDelay)
                yield return null;

            // No slot: the compare tooltip's equipment-slot and vendor-price branches are
            // keyed on the slot type, and a ground Drop is neither.
            PreviewProvider.Instance.RefreshPreviewDisplay(new Package(null, item, 1u), null);
            _previewShown = true;
        }

        /// <summary>Cancels a preview still waiting out its delay and takes down one already up.</summary>
        private void HidePreview()
        {
            StopAllCoroutines();

            if (!_previewShown)
                return;

            _previewShown = false;

            // The provider is gone while Play Mode tears the scene down.
            if (PreviewProvider.Instance != null)
                PreviewProvider.Instance.RefreshPreviewDisplay(new Package(null, null, 0u), null);
        }
    }
}
