using System.Text.RegularExpressions;
using Submodules.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Provider
{
    [DefaultExecutionOrder(0)]
    public abstract class AbstractProvider<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T instance = null;
        private static bool isQuitting = false;

        public static T Instance
        {
            get
            {
                // A teardown-ordering pitfall: something disabling during shutdown (e.g. a
                // display's OnDisable) can still ask for Instance after the real one was
                // already destroyed, which would otherwise spin up a fresh, unconfigured
                // instance that immediately leaks past scene teardown.
                if (isQuitting)
                    return instance;

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
                            Debug.LogWarning($"{instance.name.Colored(Color.yellow)} was not a root object - reparented so DontDestroyOnLoad() can persist it", instance);
                            instance.transform.SetParent(null);
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
                    instance = new GameObject(GetProviderName()).AddComponent<T>(); // this calls Awake on the new GameObject

                    Debug.Log($"Created new {instance.name.ColoredComponent()}", instance);
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

        private void OnApplicationQuit() => isQuitting = true;
    }
}
