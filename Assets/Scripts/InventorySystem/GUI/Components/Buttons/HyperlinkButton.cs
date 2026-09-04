using Submodules.Utility.Extensions;
using Submodules.Utility.UI;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Buttons
{
    public class HyperlinkButton : AbstractButton
    {
        [SerializeField] private string linkToOpen;

        protected override void OnClick()
        {
            if (Application.isPlaying)
                Application.OpenURL(linkToOpen);

            Debug.Log($"OPEN URL:\t{linkToOpen.Colored(ColorExtensions.LightBlue)}");
        }
    }
}