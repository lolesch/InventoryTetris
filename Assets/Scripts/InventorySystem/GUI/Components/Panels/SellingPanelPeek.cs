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
    /// <para>A peek with no driver cannot work, and nothing else would say so: an unset or wrong
    /// <c>driver</c> is warned about in the editor (<c>OnValidate</c>) and again when the component
    /// enables in Play.</para>
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
        private ITwoPanelDriver peekDriver;

        private static readonly List<SimplePanel> Ancestry = new();

        // Subscribed in OnEnable, not Awake: with domain reload off a scene object survives Play and Awake
        // does not run again.
        private void OnEnable()
        {
            if (driver is not ITwoPanelDriver twoPanelDriver)
            {
                Debug.LogWarning($"{name}: {DriverProblem()}", this);
                peek = null;
                peekDriver = null;
                return;
            }

            // The peek outlives a disable: it remembers that this hold has already peeked, so an Alt that is
            // still down when the component comes back does not peek a second time. A new driver is a new peek.
            if (peek == null || !ReferenceEquals(peekDriver, twoPanelDriver))
            {
                peek = new TwoPanelPeek(twoPanelDriver, AltHeld, PanelIsOpen);
                peekDriver = twoPanelDriver;
            }
        }

        // Gives the tab back and nothing else: the peek is kept (see OnEnable).
        private void OnDisable() => peek?.Release();

        private string DriverProblem() => driver
            ? $"its 'driver' ({driver.GetType().Name}) is not an ITwoPanelDriver, so the peek never works. " +
              "Name the TwoPanelToggle that drives this panel's tabs."
            : "its 'driver' is unset, so the peek never works. Name the TwoPanelToggle that drives this " +
              "panel's tabs.";

#if UNITY_EDITOR
        /// <summary>An unset or wrong driver switches the peek off without a sound, so it is said here.</summary>
        private void OnValidate()
        {
            if (driver is not ITwoPanelDriver)
                Debug.LogWarning($"{name}: {DriverProblem()}", this);
        }
#endif // UNITY_EDITOR

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
