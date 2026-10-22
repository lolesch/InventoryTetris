using Submodules.Utility.UI;
using UnityEngine;
using UnityEngine.Events;

namespace ToolSmiths.InventorySystem.GUI.Components.Buttons
{
    /// <summary>
    /// The <see cref="AbstractButton"/> that stands in for a UI Button where the scene wires the click to a
    /// method in place (the dev tools), instead of a panel owning what the click means.
    /// </summary>
    public sealed class UnityEventButton : AbstractButton
    {
        [SerializeField] private UnityEvent onClick;

        protected override void OnClick() => onClick?.Invoke();
    }
}
