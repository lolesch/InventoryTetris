﻿using TMPro;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public class TestDisplay : AbstractDisplay<TextAndFontSize>
    {
        [SerializeField] protected TextMeshProUGUI testText;

        public override void Display(TextAndFontSize newData)
        {
            testText.text = newData.text;
            testText.fontSize = newData.fontSize;
        }

        private void OnValidate()
        {
            if (testText == null)
                testText = GetComponent<TextMeshProUGUI>();

            Display(new("This is a test display - requires further implementation", testText.fontSize));
        }
    }

    public struct TextAndFontSize
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
