using Submodules.Utility.UI;
using Submodules.Utility.UI.InteractiveElements;
using UnityEngine;

namespace InventorySystem.GUI.Components.Toggles
{
    public sealed class RadioToggle : AbstractToggle
    {
        [SerializeField] private RadioGroup groupToToggle;

        protected override void ToggleSideEffects()
        {
            if (groupToToggle == null) return;
            
            if (IsOn && groupToToggle.ActivatedToggle != null)
                groupToToggle.ActivatedToggle.SetToggle(false);
            else if (!IsOn && groupToToggle.PreviouslyActivatedToggle != null)
                groupToToggle.PreviouslyActivatedToggle.SetToggle(true);
        }
    }
}