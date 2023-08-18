using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components
{
    public class DonstDestroyOnLoad : MonoBehaviour
    {
        private void Awake()
        {
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }
    }
}
