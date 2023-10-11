using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ToolSmiths.InventorySystem.Utility
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class DebugSceneName : MonoBehaviour
    {
        [SerializeField, ReadOnly] private TextMeshProUGUI sceneText;
        private TextMeshProUGUI SceneText => sceneText != null ? sceneText : sceneText = GetComponent<TextMeshProUGUI>();
        private string CurrentScene => SceneManager.GetActiveScene().name;

        private void OnEnable() => SetSceneText();

        private void OnValidate() => SetSceneText();

        private void SetSceneText()
        {
            if (Debug.isDebugBuild)
            {
                if (SceneText != null && CurrentScene != null)
                    SceneText.text = CurrentScene;
            }
        }
    }
}
