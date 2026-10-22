using Submodules.Utility.Services;
using ToolSmiths.InventorySystem.Runtime.Simulation;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Toggles
{
    /// <summary>
    /// The Inventory title's Auto Equip switch: shows and sets <c>CharacterEquipment.autoEquip</c> of the
    /// current Hero. Read every frame, not subscribed: the flag is a plain field other writers set (the
    /// debug tools), and a hero load replaces the Equipment without raising an event.
    /// </summary>
    [RequireComponent(typeof(EventToggle))]
    public sealed class AutoEquipToggle : MonoBehaviour
    {
        private EventToggle _toggle;
        private EventToggle Toggle => _toggle ? _toggle : _toggle = GetComponent<EventToggle>();

        private void OnEnable()
        {
            Toggle.Toggled += Write;
            Refresh();
        }

        private void OnDisable() => Toggle.Toggled -= Write;

        private void Update() => Refresh();

        private void Refresh()
        {
            if (ServiceLocator.IsArmed)
                Toggle.SyncToggle(Session.Instance.Hero.Equipment.autoEquip);
        }

        private static void Write(bool on)
        {
            if (ServiceLocator.IsArmed)
                Session.Instance.Hero.Equipment.autoEquip = on;
        }
    }
}
