using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components
{
    public class DontDestroyOnLoad : MonoBehaviour
    {
        private void Awake()
        {
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }
    }
}
