using TMPro;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public class TestDisplay : AbstractDisplay<string>
    {
        [SerializeField] protected TextMeshProUGUI m_TestText;
        public override void Display(string newData) => m_TestText.text = newData;

        private void OnValidate() => Display("This is a test text");
    }
}
