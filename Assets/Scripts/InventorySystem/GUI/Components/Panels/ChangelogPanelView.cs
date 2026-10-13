using System.Collections.Generic;
using Submodules.Utility.UI;
using TMPro;
using ToolSmiths.InventorySystem.Changelogs;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Components.Panels
{
    /// <summary>
    /// Fills the Changelog panel's tab bodies from the shipped Changelog file. The lines come from
    /// <see cref="ChangelogTabs"/>; this only turns them into TMP rows. The panel root stays enabled
    /// (SimplePanel fades a CanvasGroup), so the file is parsed once, when the scene loads. Patch Notes
    /// entries collapse under their heading; only the newest starts open, and closing the panel restores that.
    /// </summary>
    public sealed class ChangelogPanelView : MonoBehaviour
    {
        private const float FontSize = 18f;
        private const float HeadingFontSize = 22f;
        private const float EntrySpacing = 4f;

        [SerializeField] private TextAsset changelogFile;
        [SerializeField] private RectTransform patchNotesBody;
        [SerializeField] private RectTransform knownIssuesBody;
        [SerializeField] private RectTransform roadmapBody;

        private readonly List<ChangelogEntryHeading> headings = new();
        private SimplePanel panel;
        private bool isBound;

        private void OnEnable()
        {
            if (isBound) return;
            isBound = true;

            var development = Application.isEditor || Debug.isDebugBuild;
            var tabs = ChangelogTabs.Read(changelogFile ? changelogFile.text : string.Empty, development);
            if (tabs.Failed) Debug.LogError(tabs.FailureLog, changelogFile);

            AddEntries(patchNotesBody, tabs);
            AddRows(knownIssuesBody, tabs.KnownIssues);
            AddRows(roadmapBody, tabs.Roadmap);
        }

        /// <summary>Unity calls this on every CanvasGroup change above, so on each frame of a fade;
        /// a collapsed panel is closed for good, whichever button closed it.</summary>
        private void OnCanvasGroupChanged()
        {
            if (!panel) panel = GetComponent<SimplePanel>();
            if (panel && panel.IsCollapsed) ResetEntries();
        }

        /// <summary>Only the newest entry (the first shown) is open.</summary>
        private void ResetEntries()
        {
            for (var i = 0; i < headings.Count; i++) headings[i].SetExpanded(i == 0);
        }

        /// <summary>Each entry is its own object: a heading button, then the bullet container it shows and hides.</summary>
        private void AddEntries(RectTransform body, ChangelogTabs tabs)
        {
            if (tabs.PatchNotesPlaceholder != null) AddRows(body, new[] { tabs.PatchNotesPlaceholder });

            for (var i = 0; i < tabs.Entries.Count; i++)
            {
                var entry = tabs.Entries[i];
                var group = AddContainer("Entry", body, EntrySpacing);
                var heading = AddRow(group, "Heading", entry.Heading, HeadingFontSize, FontStyles.Bold);
                var bullets = AddContainer("Bullets", group, 0f);
                AddRows(bullets, entry.Lines);

                var button = heading.gameObject.AddComponent<ChangelogEntryHeading>();
                heading.raycastTarget = true;
                heading.rectTransform.pivot = new Vector2(0f, .5f);
                button.Bind(entry.Heading, bullets, expanded: i == 0);
                headings.Add(button);
            }
        }

        private static RectTransform AddContainer(string name, RectTransform parent, float spacing)
        {
            var container = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            container.transform.SetParent(parent, false);

            var layout = container.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return (RectTransform)container.transform;
        }

        private static void AddRows(RectTransform body, IReadOnlyList<string> lines)
        {
            foreach (var line in lines) AddRow(body, "Line", line, FontSize, FontStyles.Normal);
        }

        private static TextMeshProUGUI AddRow(RectTransform parent, string name, string line, float fontSize, FontStyles style)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            row.transform.SetParent(parent, false);

            var text = row.GetComponent<TextMeshProUGUI>();
            text.text = line;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }
    }
}
