using System;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Extensions
{
    public static class Extensions
    {
        public static float Map(this float value, float fromMin, float fromMax, float toMin = 0, float toMax = 1) => (value - fromMin) / (fromMax - fromMin) * (toMax - toMin) + toMin;
        public static float MapTo01(this float value, float fromMin, float fromMax) => (value - fromMin) / (fromMax - fromMin);
        public static float MapFrom01(this float value, float toMin, float toMax) => value * (toMax - toMin) + toMin;
        public static string Colored(this string text, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{text}</color>";
        public static string SplitCamelCase(this Enum input) => System.Text.RegularExpressions.Regex.Replace(input.ToString(), "([A-Z])", " $1", System.Text.RegularExpressions.RegexOptions.Compiled).Trim();
    }
}
