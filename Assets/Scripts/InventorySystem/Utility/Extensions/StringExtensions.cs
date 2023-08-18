using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Utility.Extensions
{
    public static class StringExtensions
    {
        public static bool HasValue([NotNullWhen(false)] this string value) => !string.IsNullOrWhiteSpace(value);

        /// <summary>
        /// An inline richtext color converter
        /// </summary>
        public static string Colored(this string text, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{text}</color>";
        public static string ColoredComponent(this string text) => Colored(text, ColorExtensions.LightBlue);
        public static string ColoredComponent(this GameObject gameObject) => ColoredComponent(gameObject.name);

        public static string SplitCamelCase(this Enum input) => System.Text.RegularExpressions.Regex.Replace(input.ToString(), "([A-Z])", " $1", System.Text.RegularExpressions.RegexOptions.Compiled).Trim();
    }
}