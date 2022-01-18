using TeppichsTools.Logging;
using UnityEngine;

namespace TeppichsTools.Creation
{
    public class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T s_instance;

        public static T Instance
        {
            get
            {
                if (s_instance)
                    return s_instance;

                T[] candidates = FindObjectsOfType<T>();

                if (candidates.Length == 0)
                    s_instance = new GameObject("MonoSingleton_" + nameof(T)).AddComponent<T>();
                else
                {
                    s_instance = candidates[0];

                    for (int i = 1; i < candidates.Length; i++)
                    {
                        EditorDebug.LogWarning("Found multiple instances of the MonoSingleton of type " + typeof(T));
#if UNITY_EDITOR
                        DestroyImmediate(candidates[i]);
#else
                        Destroy(candidates[i]);
#endif
                    }
                }
#if !UNITY_EDITOR
                DontDestroyOnLoad(s_instance.gameObject);
#endif
                return s_instance;
            }
        }
    }
}