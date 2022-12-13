using UnityEngine;

namespace TeppichsTools.Creation
{
    public class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T instance;

        public static T Instance
        {
            get
            {
                if (!instance)
                {
                    var candidates = FindObjectsOfType<T>();

                    if (0 < candidates.Length)
                    {
                        instance = candidates[0];

                        for (var i = candidates.Length; i-- > 0;)
                        {
                            if (candidates[i] != null && candidates[i] != instance)
                            {
#if UNITY_EDITOR
                                DestroyImmediate(candidates[i]);
#else
                                Destroy(candidates[i]);
#endif
                                Debug.LogWarning($"Destroyed obsolete instance of {typeof(T)} - Instance");
                            }
                        }
                    }
                    else if (Application.isPlaying)
                    {
                        instance = new GameObject($"{typeof(T).Name}".ToUpper()).AddComponent<T>();

                        Debug.LogWarning($"Created new instance of {typeof(T)}");
                    }

                    if (Application.isPlaying)
                        // TODO: make this a new root object
                        DontDestroyOnLoad(instance.gameObject);
                }

                return instance;
            }
        }

        private void Awake() => name = $"{typeof(T).Name}".ToUpper();
    }
}