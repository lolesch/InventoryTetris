using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components
{
    [RequireComponent(typeof(RectTransform))]
    public class RefreshLayoutOnEnable : MonoBehaviour
    {
        //private void Start() => (transform as RectTransform).RefreshContentFitter();

        private void OnEnable() => (transform as RectTransform).RefreshContentFitter();
    }
}