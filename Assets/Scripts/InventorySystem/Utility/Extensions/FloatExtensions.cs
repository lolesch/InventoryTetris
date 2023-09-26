

using UnityEngine;

namespace ToolSmiths.InventorySystem.Utility.Extensions
{
    public static class FloatExtensions
    {
        public static float Map(this float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            if (fromMax - fromMin == 0)
            {
                Debug.LogWarning($"Cannot devide by 0! {fromMin} needs to differ {fromMax}");
                fromMax++;
            }

            return (value - fromMin) / (fromMax - fromMin) * (toMax - toMin) + toMin;
        }

        public static float MapTo01(this float value, float fromMin, float fromMax) => Map(value, fromMin, fromMax, 0, 1);
        public static float MapFrom01(this float value, float toMin, float toMax) => Map(value, 0, 1, toMin, toMax);

        public static float Map(this float value, Vector2 from, float toMin, float toMax) => Map(value, from.x, from.y, toMin, toMax);
        public static float Map(this float value, float fromMin, float fromMax, Vector2 to) => Map(value, fromMin, fromMax, to.x, to.y);
        public static float Map(this float value, Vector2 from, Vector2 to) => Map(value, from.x, from.y, to.x, to.y);
    }
}
