using System.Collections.Generic;
using TMPro;
using ToolSmiths.InventorySystem.Changelogs;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Components.Panels
{
    /// <summary>
    /// Fills the Changelog panel's tab bodies from the shipped Changelog file. The lines come from
    /// <see cref="ChangelogTabs"/>; this only turns them into TMP rows. The panel root stays enabled
    /// (SimplePanel fades a CanvasGroup), so the file is parsed once, when the scene loads.
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

        private bool isBound;

        private void OnEnable()
        {
            if (isBound) return;
            isBound = true;

            var tabs = ChangelogTabs.Read(changelogFile ? changelogFile.text : string.Empty);
            if (tabs.Failed) Debug.LogError(tabs.FailureLog, changelogFile);

            AddEntries(patchNotesBody, tabs);
            AddRows(knownIssuesBody, tabs.KnownIssues);
            AddRows(roadmapBody, tabs.Roadmap);
        }

        /// <summary>Each entry is its own object (heading row, then a bullet container), so an entry can later collapse as a group.</summary>
        private static void AddEntries(RectTransform body, ChangelogTabs tabs)
        {
            if (tabs.PatchNotesPlaceholder != null) AddRows(body, new[] { tabs.PatchNotesPlaceholder });

            foreach (var entry in tabs.Entries)
            {
                var group = AddContainer("Entry", body, EntrySpacing);
                AddRow(group, "Heading", entry.Heading, HeadingFontSize, FontStyles.Bold);
                AddRows(AddContainer("Bullets", group, 0f), entry.Lines);
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

        private static void AddRow(RectTransform parent, string name, string line, float fontSize, FontStyles style)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            row.transform.SetParent(parent, false);

            var text = row.GetComponent<TextMeshProUGUI>();
            text.text = line;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
        }
    }
}
