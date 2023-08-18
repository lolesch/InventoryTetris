using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DC
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class DebugSceneName : MonoBehaviour
    {
        private TextMeshProUGUI sceneText;
        [SerializeField, ReadOnly] private string sceneName;
        private void OnEnable() => SetSceneText();

        private void OnValidate() => SetSceneText();

        private void SetSceneText()
        {
            if (Debug.isDebugBuild)
            {
                sceneText = GetComponent<TextMeshProUGUI>();

                sceneName = SceneManager.GetActiveScene().name;

                if (sceneText != null && sceneName != null)
                    sceneText.text = sceneName;
            }
        }
    }
}
