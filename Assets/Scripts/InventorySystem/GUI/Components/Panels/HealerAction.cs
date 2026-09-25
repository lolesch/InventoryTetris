using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Panels
{
    /// <summary>
    /// The Healer's on-transition side effect (issue #58): a full Health and Resource refill.
    /// Lives beside <see cref="SidePanel"/> rather than inside
    /// <see cref="ToolSmiths.InventorySystem.GUI.Components.Toggles.SidePanelToggle"/>, whose
    /// <c>OnToggle</c> is deliberately empty - a side effect like healing is not "does the panel
    /// show", the one question that class answers.
    ///
    /// <para>Reacts to <see cref="InventoryProvider"/>'s context event rather than the toggle's
    /// click: <see cref="InventoryContextState.Changed"/> only fires on an actual change, so a
    /// re-click that the <c>RadioGroup</c> turns into "switch off" never reaches here - the
    /// refill fires once per genuine entry into <see cref="InventoryContext.Healer"/>, never on
    /// the way out.</para>
    ///
    /// <para>No hard reference to the hero: <see cref="CharacterProvider.HealPlayer"/> resolves
    /// the live player itself, the same seam <c>KillPlayer</c> already uses.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HealerAction : MonoBehaviour
    {
        private void OnEnable()
        {
            _ = InventoryProvider.TrySubscribeContextChanged(OnContextChanged, out _);
        }

        private void OnDisable()
        {
            InventoryProvider.UnsubscribeContextChanged(OnContextChanged);
        }

        private void OnContextChanged(InventoryContext context)
        {
            if (context != InventoryContext.Healer)
                return;

            CharacterProvider.Instance.HealPlayer();
        }
    }
}
