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
        private string title;

        public bool IsExpanded => bullets.gameObject.activeSelf;

        public void Bind(string headingText, RectTransform bulletContainer, bool expanded)
        {
            label = GetComponent<TextMeshProUGUI>();
            title = headingText;
            bullets = bulletContainer;
            Apply(expanded);
        }

        public void SetExpanded(bool expanded)
        {
            if (IsExpanded == expanded) return;

            Apply(expanded);
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)bullets.parent.parent);
        }

        protected override void OnClick() => SetExpanded(!IsExpanded);

        private void Apply(bool expanded)
        {
            bullets.gameObject.SetActive(expanded);
            label.text = (expanded ? OpenMark : ClosedMark) + title;
        }
    }
}
