using System.Collections.Generic;
using Submodules.Utility.SerializeInterface;
using Submodules.Utility.UI;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Panels
{
    /// <summary>
    /// The selling panels' (the Vendor, the Healer) Alt key for their <see cref="TwoPanelPeek"/>. One per
    /// panel, on the panel's own object, naming the driver of the panel's two-panel switch by a serialized
    /// reference. It answers the peek's two questions - is Alt held (<see cref="ModifierKeys.Alt"/>, the
    /// helper the tooltip's roll-range modifier reads, so both agree on what Alt is) and is the panel open -
    /// and hands the app losing focus on, because an Alt+Tab never sends the key up.
    ///
    /// <para>Alt stays "show me more": the tooltip's roll-range modifier fires with it on a hover, which is
    /// accepted.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SellingPanelPeek : MonoBehaviour
    {
        [SerializeField, RequireInterface(typeof(ITwoPanelDriver))]
        private Object driver;

        private ITwoPanelPeek peek;

        private static readonly List<SimplePanel> Ancestry = new();

        // Subscribed in OnEnable, not Awake: with domain reload off a scene object survives Play and Awake
        // does not run again.
        private void OnEnable() =>
            peek = driver is ITwoPanelDriver twoPanelDriver
                ? new TwoPanelPeek(twoPanelDriver, AltHeld, PanelIsOpen)
                : null;

        private void OnDisable()
        {
            peek?.Release();
            peek = null;
        }

        private void Update() => peek?.Tick();

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                peek?.Release();
        }

        private static bool AltHeld() => Application.isFocused && ModifierKeys.Alt;

        /// <summary>A panel stays enabled when hidden (its alpha is 0), so "open" is every panel from this
        /// one up being reachable.</summary>
        private bool PanelIsOpen()
        {
            GetComponentsInParent<SimplePanel>(true, Ancestry);

            foreach (var panel in Ancestry)
                if (!panel.IsInteractive)
                    return false;

            return 0 < Ancestry.Count;
        }
    }
}
