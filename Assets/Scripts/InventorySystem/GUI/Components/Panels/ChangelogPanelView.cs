using System.Collections.Generic;
using Submodules.Utility.UI;
using TMPro;
using ToolSmiths.InventorySystem.Changelogs;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Components.Panels
{
    /// <summary>
    /// Fills the Changelog panel's tab bodies from the shipped file on every enable and removes the rows on disable,
    /// so a scene object that survives a Play session (reload disabled) never keeps stale or saved-in rows.
    /// </summary>
    [RequireComponent(typeof(SimplePanel))]
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
        private readonly List<GameObject> rows = new();
        private SimplePanel panel;

        private void Awake() => panel = GetComponent<SimplePanel>();

        private void OnEnable()
        {
            var tabs = ChangelogTabs.Read(changelogFile ? changelogFile.text : string.Empty, Debug.isDebugBuild);
            if (tabs.Failed) Debug.LogError(tabs.FailureLog, changelogFile);

            AddEntries(patchNotesBody, tabs);
            AddRows(knownIssuesBody, tabs.KnownIssues);
            AddRows(roadmapBody, tabs.Roadmap);
        }

        private void OnDisable()
        {
            foreach (var row in rows)
                if (row) DestroyRow(row);

            rows.Clear();
            headings.Clear();
        }

        /// <summary>Unity calls this on every CanvasGroup change above, so on each frame of a fade;
        /// a collapsed panel is closed for good, whichever button closed it. SimplePanel's own Awake
        /// can run before our Awake, while <c>panel</c> is still null.</summary>
        private void OnCanvasGroupChanged()
        {
            if (panel && panel.IsCollapsed) ResetEntries();
        }

        /// <summary>Entries collapse under their heading; only the newest (the first shown) starts open.</summary>
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
                button.Bind(entry.Heading, bullets, body, expanded: i == 0);
                headings.Add(button);
            }
        }

        private static void DestroyRow(GameObject row)
        {
            if (Application.isPlaying) Destroy(row);
            else DestroyImmediate(row);
        }

        private RectTransform AddContainer(string name, RectTransform parent, float spacing)
        {
            var container = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            container.transform.SetParent(parent, false);
            rows.Add(container);

            var layout = container.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return (RectTransform)container.transform;
        }

        private void AddRows(RectTransform body, IReadOnlyList<string> lines)
        {
            foreach (var line in lines) AddRow(body, "Line", line, FontSize, FontStyles.Normal);
        }

        private TextMeshProUGUI AddRow(RectTransform parent, string name, string line, float fontSize, FontStyles style)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            row.transform.SetParent(parent, false);
            rows.Add(row);

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
