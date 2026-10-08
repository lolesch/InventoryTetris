using Submodules.Utility.UI;
using System.Linq;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI
{
    /// <summary>
    /// PROTOTYPE - while Alt is held the open selling panel shows its Sold tab; letting go returns
    /// to the tab it was on. The spec left tab switching out ("a sale does not switch tabs"); this
    /// is not part of epic #124. Alt is also the tooltip's roll-range modifier
    /// (<see cref="ModifierKeys.Alt"/>), so the two fire together on a hover.
    ///
    /// <para>Self-installing, no scene wiring. The switch is the checkbox in the screen's
    /// bottom-left corner (editor and development builds), remembered across runs. Delete this
    /// file to drop the prototype.</para>
    /// </summary>
    public sealed class SoldTabPrototype : MonoBehaviour
    {
        private const string PeekPref = "SoldTabPrototype.Peek";

        /// <summary>The Sold tab's toggle in both selling panels' tab group. By name: a prototype
        /// does not earn a serialized reference in two scenes' worth of wiring.</summary>
        private const string SoldToggleName = "SoldItems";

        [SerializeField] private bool peekWhileAltHeld = true;

        private bool altWasHeld;
        private ToggleGroup peekGroup;
        private AbstractToggle peekFrom;
        private AbstractToggle peekTo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var host = new GameObject(nameof(SoldTabPrototype));
            DontDestroyOnLoad(host);
            _ = host.AddComponent<SoldTabPrototype>();
        }

        private void Awake() => peekWhileAltHeld = PlayerPrefs.GetInt(PeekPref, 1) != 0;

        private void Update()
        {
            var altHeld = peekWhileAltHeld && Application.isFocused && ModifierKeys.Alt;

            if (altHeld == altWasHeld)
                return;

            altWasHeld = altHeld;

            if (altHeld)
                BeginPeek();
            else
                EndPeek();
        }

        private void BeginPeek()
        {
            foreach (var (group, sold) in OpenSellingTabs())
            {
                if (group.ActiveMember == sold)
                    continue;

                peekGroup = group;
                peekFrom = group.ActiveMember;
                peekTo = sold;
                sold.SetToggle(true);
                return;
            }
        }

        private void EndPeek()
        {
            // The panel closing resets its tab: only undo a peek that is still showing.
            if (peekGroup && peekFrom && peekGroup.ActiveMember == peekTo)
                peekFrom.SetToggle(true);

            peekGroup = null;
            peekFrom = null;
            peekTo = null;
        }

        /// <summary>The tab groups of the selling panels that are showing right now. A panel stays
        /// enabled when hidden (its alpha is 0), so "open" is every ancestor panel being reachable.</summary>
        private static (ToggleGroup group, AbstractToggle sold)[] OpenSellingTabs() =>
            FindObjectsByType<ToggleGroup>(FindObjectsSortMode.None)
                .Where(group => group.GetComponentsInParent<SimplePanel>().All(panel => panel.IsInteractive))
                .Select(group => (group, sold: group.GetComponentsInChildren<AbstractToggle>(true)
                    .FirstOrDefault(toggle => toggle.name == SoldToggleName && toggle.RadioGroup == group)))
                .Where(tab => tab.sold)
                .ToArray();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(8, Screen.height - 34, 230, 28), UnityEngine.GUI.skin.box);

            var peek = GUILayout.Toggle(peekWhileAltHeld, "Sold tab: peek while Alt held");

            GUILayout.EndArea();

            if (peek == peekWhileAltHeld)
                return;

            peekWhileAltHeld = peek;
            PlayerPrefs.SetInt(PeekPref, peek ? 1 : 0);
        }
#endif
    }
}
