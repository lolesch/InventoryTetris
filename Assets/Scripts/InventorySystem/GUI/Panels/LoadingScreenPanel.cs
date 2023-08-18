using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Panels
{
    public class LoadingScreenPanel : AbstractPanel
    {
        [SerializeField] private Image progressionBar;
        [SerializeField] private TextMeshProUGUI progressionText;
        [SerializeField] private TextMeshProUGUI sceneToLoadText;

        public void SetLoadingProgression(float progress)
        {
            if (progressionBar)
            {
                progressionBar.fillAmount = progress;

                if (progressionText)
                    progressionText.text = $"{progress:P0}";
            }
        }

        public void SetLoadingText(string text)
        {
            if (sceneToLoadText)
                sceneToLoadText.text = $"loading: {text}";
        }
    }
}
