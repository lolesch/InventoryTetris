using TMPro;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public class TestDisplay : AbstractDisplay<TextAndFontSize>
    {
        [SerializeField] protected TextMeshProUGUI m_TestText;

        public override void Display(TextAndFontSize newData)
        {
            m_TestText.text = newData.text;
            m_TestText.fontSize = newData.fontSize;
        }

        private void OnValidate()
        {
            if (m_TestText == null)
                m_TestText = GetComponent<TextMeshProUGUI>();

            //Display(new("This is a test text", m_TestText.fontSize));
        }
    }

    public class TextAndFontSize
    {
        public string text;
        public float fontSize;

        public TextAndFontSize(string text, float fontSize)
        {
            this.text = text;
            this.fontSize = fontSize;
        }
    }
}
