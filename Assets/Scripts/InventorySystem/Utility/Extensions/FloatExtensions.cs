﻿namespace ToolSmiths.InventorySystem.Utility.Extensions
{
    public static class FloatExtensions
    {
        public static float Map(this float value, float fromMin, float fromMax, float toMin = 0, float toMax = 1) => (value - fromMin) / (fromMax - fromMin) * (toMax - toMin) + toMin;
        public static float MapTo01(this float value, float fromMin, float fromMax) => (value - fromMin) / (fromMax - fromMin);
        public static float MapFrom01(this float value, float toMin, float toMax) => value * (toMax - toMin) + toMin;
    }
}
