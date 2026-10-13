using System.Collections.Generic;
using TMPro;
using ToolSmiths.InventorySystem.Changelogs;
using UnityEngine;

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

        [SerializeField] private TextAsset changelogFile;
        [SerializeField] private RectTransform knownIssuesBody;
        [SerializeField] private RectTransform roadmapBody;

        private bool isBound;

        private void OnEnable()
        {
            if (isBound) return;
            isBound = true;

            var tabs = ChangelogTabs.Read(changelogFile ? changelogFile.text : string.Empty);
            if (tabs.Failed) Debug.LogError(tabs.FailureLog, changelogFile);

            AddRows(knownIssuesBody, tabs.KnownIssues);
            AddRows(roadmapBody, tabs.Roadmap);
        }

        private static void AddRows(RectTransform body, IReadOnlyList<string> lines)
        {
            foreach (var line in lines)
            {
                var row = new GameObject("Line", typeof(RectTransform), typeof(TextMeshProUGUI));
                row.transform.SetParent(body, false);

                var text = row.GetComponent<TextMeshProUGUI>();
                text.text = line;
                text.fontSize = FontSize;
                text.textWrappingMode = TextWrappingModes.Normal;
                text.raycastTarget = false;
            }
        }
    }
}
