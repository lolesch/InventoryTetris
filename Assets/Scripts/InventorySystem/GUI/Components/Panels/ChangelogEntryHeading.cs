using Submodules.Utility.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Components.Panels
{
    /// <summary>A Patch Notes entry's heading row: a menu button that shows or hides the entry's bullets.</summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    internal sealed class ChangelogEntryHeading : AbstractButton
    {
        private const string OpenMark = "[-] ";
        private const string ClosedMark = "[+] ";

        private TextMeshProUGUI label;
        private RectTransform bullets;
        private RectTransform layoutRoot;
        private string title;

        public bool IsExpanded => bullets.gameObject.activeSelf;

        /// <param name="layoutToRebuild">The layout that has to reflow when the bullets show or hide.</param>
        public void Bind(string headingText, RectTransform bulletContainer, RectTransform layoutToRebuild, bool expanded)
        {
            label = GetComponent<TextMeshProUGUI>();
            title = headingText;
            bullets = bulletContainer;
            layoutRoot = layoutToRebuild;
            Apply(expanded);
        }

        public void SetExpanded(bool expanded)
        {
            if (IsExpanded == expanded) return;

            Apply(expanded);
            LayoutRebuilder.MarkLayoutForRebuild(layoutRoot);
        }

        protected override void OnClick() => SetExpanded(!IsExpanded);

        private void Apply(bool expanded)
        {
            bullets.gameObject.SetActive(expanded);
            label.text = (expanded ? OpenMark : ClosedMark) + title;
        }
    }
}
