using TMPro;
using ToolSmiths.InventorySystem.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public class CharacterDPSDisplay : MonoBehaviour, IDisplay<DPSData>
    {
        [SerializeField] protected Image icon;

        [SerializeField] protected TextMeshProUGUI text;

        public void RefreshDisplay(DPSData data)
        {
            icon.sprite = data.icon;
            text.text = data.displayText;
        }
    }
}