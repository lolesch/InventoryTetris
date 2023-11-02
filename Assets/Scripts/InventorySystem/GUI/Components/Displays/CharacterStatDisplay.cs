using TMPro;
using ToolSmiths.InventorySystem.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public class CharacterStatDisplay : MonoBehaviour, IDisplay<CharacterStatData>
    {
        [SerializeField] protected Image icon;

        [SerializeField] protected TextMeshProUGUI text;

        public void RefreshDisplay(CharacterStatData newData)
        {
            icon.sprite = newData.icon;
            text.text = newData.displayText;
        }
    }
}