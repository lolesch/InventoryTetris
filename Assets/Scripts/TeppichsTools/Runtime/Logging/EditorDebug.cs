using System;
using UnityEngine;

namespace TeppichsTools.Logging
{
    public static class EditorDebug
    {
        #region Lines

        public static void DrawLine(Vector3 start, Vector3 end, Color color = default, float duration = 0.0f,
                                    bool    depthTest = true)
        {
#if UNITY_EDITOR
            if (color == default)
                color = Color.white;

            Debug.DrawLine(start, end, color, duration, depthTest);
#endif
        }

        #endregion

        #region Logs

        public static void Log(object message) => DoLog(message, Debug.Log);

        public static void LogError(object message) => DoLog(message, Debug.LogError);

        public static void LogWarning(object message) => DoLog(message, Debug.LogWarning);

        private static void DoLog(object message, Action<object> log)
        {
#if UNITY_EDITOR
            log(message);
#endif
        }

        #endregion
    }
}