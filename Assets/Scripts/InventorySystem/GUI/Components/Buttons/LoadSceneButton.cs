using ToolSmiths.InventorySystem.Runtime.Provider;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Buttons
{
    public class LoadSceneButton : AbstractButton
    {
        [Space]
        [SerializeField, SceneRef] protected string sceneToLoad;

        protected override void OnClick() => SceneProvider.Instance.LoadScene(sceneToLoad);
    }
}
