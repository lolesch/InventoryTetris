using System.Text.RegularExpressions;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Provider
{
    [DefaultExecutionOrder(0)]
    public abstract class AbstractProvider<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T instance = null;

        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    if (!InstanceExists())
                        CreateNewInstance();

                    if (Application.isPlaying)
                        DontDestroyOnLoad(instance.gameObject);
                }

                return instance;

                static bool InstanceExists()
                {
                    var candidates = FindObjectsOfType<T>();

                    if (0 < candidates.Length)
                    {
                        instance = candidates[0];
                        instance.name = GetProviderName();

                        Debug.Log($"Found existing {instance.name.ColoredComponent()}", instance);

                        /// instance as component of "non-root" gameObjects
                        if (instance.transform.parent != null)
                        {
                            Debug.LogWarning($"{instance.name.Colored(Color.yellow)} is no root object and can't be moved by DonstDestroyOnLoad", instance);
                            // concider force reparent the GameObject as root
                        }

                        DisableRemainingCandidates(candidates);

                        return true;
                    }
                    return false;

                    static void DisableRemainingCandidates(T[] candidates)
                    {
                        for (var i = candidates.Length; i-- > 1;)
                        {
                            if (candidates[i] != null)
                            {
                                Debug.Log($"Disabled {instance.name.Colored(Color.red)} because there is already an Instance!", candidates[i]);

                                candidates[i].enabled = false;
                                //#if UNITY_EDITOR
                                //                                DestroyImmediate(candidates[i]);
                                //#else
                                //                                Destroy(candidates[i]);
                                //#endif
                            }
                        }
                    }
                }

                static void CreateNewInstance()
                {
                    var gameObject = new GameObject(GetProviderName());
                    instance = gameObject.AddComponent<T>(); // this calls Awake on the new GameObject
                    //instance = new GameObject(GetProviderName()).AddComponent<T>(); // this calls Awake on the new GameObject

                    Debug.LogWarning($"Created new {instance.name.ColoredComponent()}", instance);
                }
            }
        }

        private static string GetProviderName() => Regex.Replace(typeof(T).Name, "(?<=[a-z])([A-Z])", "_$1", RegexOptions.Compiled).ToUpper();

        private void Start()
        {
            if (Instance != this)
            {
                Debug.Log($"Disabled {instance.name.Colored(Color.red)} because there is already an Instance!", Instance);

                enabled = false;

                //#if UNITY_EDITOR
                //                DestroyImmediate(this);
                //#else
                //                Destroy(this);
                //#endif
            }
        }

        protected void Reset() => name = GetProviderName();
    }
}
