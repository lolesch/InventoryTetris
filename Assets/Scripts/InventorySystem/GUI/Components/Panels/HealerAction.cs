using Submodules.Utility.Extensions;
using Submodules.Utility.Tools.Tweening;
using ToolSmiths.InventorySystem.Inventories;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Panels
{
    /// <summary>
    /// The Healer's on-transition side effect (issue #58): a full Health and Resource refill,
    /// plus a brief scale punch for feedback. Lives beside <see cref="SidePanel"/> rather than
    /// inside <see cref="ToolSmiths.InventorySystem.GUI.Components.Toggles.SidePanelToggle"/>,
    /// whose <c>OnToggle</c> is deliberately empty - a side effect like healing is not "does the
    /// panel show", the one question that class answers.
    ///
    /// <para>Reacts to <see cref="InventoryProvider"/>'s context event rather than the toggle's
    /// click: <see cref="InventoryContextState.Changed"/> only fires on an actual change, so a
    /// re-click that the <c>RadioGroup</c> turns into "switch off" never reaches here - the
    /// refill fires once per genuine entry into <see cref="InventoryContext.Healer"/>, never on
    /// the way out.</para>
    ///
    /// <para>No hard reference to the hero: <see cref="CharacterProvider.HealPlayer"/> resolves
    /// the live player itself, the same seam <c>KillPlayer</c> already uses.</para>
    ///
    /// <para><b>Punches a dedicated <see cref="icon"/>, never the toggle's own transform.</b>
    /// <see cref="Submodules.Utility.UI.InteractiveElement.Scale"/> already tweens the toggle's
    /// <c>targetGraphic.transform</c> for hover/press/selected feedback - on this toggle that
    /// graphic lives on the same <c>GameObject</c> the toggle script does. A second, uncoordinated
    /// tween racing it on <c>localScale</c> would fight that one and could strand the toggle at
    /// scale 1 instead of the selected <c>hoverScale</c> once the punch's own "back to 1" leg
    /// lands after the selection tween's shorter fade completes.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HealerAction : MonoBehaviour
    {
        [Tooltip("The icon punched on activation. Must not be the toggle's own targetGraphic " +
                 "transform - that one is already tweened by the toggle's own hover/select " +
                 "feedback, and a second tween racing it there can strand the wrong scale.")]
        [SerializeField] private RectTransform icon;

        [Tooltip("Peak scale of the punch feedback played on activation.")]
        [SerializeField] private float punchScale = 1.15f;

        [Tooltip("Duration of each half of the punch (out and back).")]
        [SerializeField] private float punchDuration = 0.12f;

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
            if (context != InventoryContext.Healer || icon == null)
                return;

            CharacterProvider.Instance.HealPlayer();

            icon.TweenScale(punchScale, punchDuration, Ease.OutQuad)
                .OnComplete(() => icon.TweenScale(1f, punchDuration, Ease.InQuad));
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (icon == null)
                Debug.LogWarning($"{name}: HealerAction has no icon - activation will heal " +
                                 "with no visual feedback (issue #58).", gameObject);
        }
#endif // UNITY_EDITOR
    }
}
