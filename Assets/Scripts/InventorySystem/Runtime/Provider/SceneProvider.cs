using System;
using System.Collections;
using ToolSmiths.InventorySystem.GUI.Components.Canvases;
using ToolSmiths.InventorySystem.GUI.Panels;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Provider
{
    [RequireComponent(typeof(RootCanvas), typeof(LoadingScreenPanel), typeof(Image))]
    public class SceneProvider : AbstractProvider<SceneProvider>
    {
        private AsyncOperation loaderOperation = null;

        private LoadingScreenPanel display = null;
        private LoadingScreenPanel Display => display != null ? display : display = GetComponent<LoadingScreenPanel>();

        private float delta;
        private float timeStamp;
        private float targetProgress;
        private float currentProgress;

        public event Action<string> OnSceneLoaded;

        private void OnEnable()
        {
            var rootCanvas = GetComponent<RootCanvas>();
            rootCanvas.name = "[LOADING_SCREEN]";
            rootCanvas.Canvas.sortingOrder = 1;

            var image = GetComponent<Image>();
            image.color = Color.black;

            Display.FadeOut();
        }

        public void LoadScene(string sceneToLoad, bool showProgress = true)
        {
            if (loaderOperation != null)
            {
                Debug.LogError("Can't load a sceneName while another is still loading");
                return;
            }
            else if (string.IsNullOrWhiteSpace(sceneToLoad))
            {
                Debug.LogError($"Cant load '{sceneToLoad}' as sceneName");
                //LoadMainMenu();
            }
            else
            {
                _ = StartCoroutine(LoadNextScene(sceneToLoad, showProgress));
            }
        }

        private IEnumerator LoadNextScene(string sceneToLoad, bool showProgress = true)
        {
            yield return null;

            //if (sceneToLoad == ApplicationProvider.Instance.bootstrapperScene)
            //{
            //    Debug.LogError($"Cannot load {sceneToLoad.Colored(ColorExtensions.LightBlue)} again after application initialisation");
            //    yield break;
            //}

            Debug.Log($"{"LOADING:".Colored(ColorExtensions.Orange)}\t{sceneToLoad.Colored(ColorExtensions.LightBlue)}");

            if (Display && showProgress)
            {
                Display.SetLoadingProgression(0);
                Display.SetLoadingText(sceneToLoad);
                Display.FadeIn();

                timeStamp = Time.unscaledTime;

                /// Wait for FadeIn
                yield return new WaitWhile(() => Time.unscaledTime - timeStamp < Display.FadeDuration);

                if (loaderOperation != null)
                {
                    Debug.LogError("Can't load a sceneName while another is still loading");
                    Display.FadeOut();
                    yield break;
                }

                loaderOperation = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Single);
                loaderOperation.allowSceneActivation = false;

                currentProgress = 0;

                /// Wait for progressionBar
                while (currentProgress < 1)
                {
                    yield return null;

                    /// map progress (from 0f to .9f) to 0f to 1f
                    targetProgress = Mathf.Clamp01(loaderOperation.progress / .9f);

                    /// Wait for the new sceneName to preload
                    if (targetProgress < .5f)
                        targetProgress *= 0.25f;

                    delta += Time.unscaledDeltaTime / 4;

                    currentProgress = Mathf.Clamp01(Mathf.Lerp(currentProgress, targetProgress, delta));

                    Display.SetLoadingProgression(currentProgress);

                    if (targetProgress == 1 && loaderOperation.allowSceneActivation == false)
                        loaderOperation.allowSceneActivation = true;
                }

                yield return new WaitForSeconds(.5f);

                Display.FadeOut();

                yield return null;

                loaderOperation = null;
            }
            else
            {
                LogExtensions.MissingComponent(nameof(LoadingScreenPanel), gameObject);

                loaderOperation = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Single);

                loaderOperation.allowSceneActivation = false;

                yield return new WaitWhile(() => (loaderOperation.progress / .9f) < 1);

                loaderOperation.allowSceneActivation = true;

                yield return null;

                loaderOperation = null;
            }

            OnSceneLoaded?.Invoke(sceneToLoad);
        }

        //[ContextMenu("Load MainMenu")]
        //public void LoadMainMenu() => LoadScene(Constants.MainMenuScene);
    }
}